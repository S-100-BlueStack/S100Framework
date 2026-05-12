using System.Globalization;
using System.Text.Json;
using S100Framework.REST.Abstractions;
using S100Framework.REST.Models;

namespace S100Framework.REST.Extensions;

/// <summary>
/// Provides state-aware convenience helpers for creating feature service replicas.
/// </summary>
public static class FeatureServiceClientCreateReplicaStateExtensions
{
    /// <summary>
    /// Creates a replica, downloads the produced file, and returns a persistable synchronization state.
    /// </summary>
    /// <param name="client">
    /// The feature service client.
    /// </param>
    /// <param name="request">
    /// The create replica request.
    /// </param>
    /// <param name="pollingOptions">
    /// Polling options used when the service returns an asynchronous status URL.
    /// </param>
    /// <param name="cancellationToken">
    /// The cancellation token.
    /// </param>
    /// <returns>
    /// The downloaded replica file and the initial synchronization state.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="client" /> or <paramref name="request" /> is <see langword="null" />.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the request is not state-compatible, when URL transport is not used, when the job fails,
    /// or when the completed result does not expose generation information required to build state.
    /// </exception>
    /// <exception cref="TimeoutException">
    /// Thrown when an asynchronous job does not complete within the configured timeout.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// Thrown when the caller cancels the operation.
    /// </exception>
    public static async Task<CreateReplicaStateResult> CreateReplicaStateAsync(
        this IFeatureServiceClient client,
        CreateReplicaRequest request,
        ReplicaPollingOptions? pollingOptions = null,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(request);

        if (request.TransportType != CreateReplicaTransportType.Url) {
            throw new InvalidOperationException(
                "CreateReplicaStateAsync requires TransportType.Url because it downloads the replica result file.");
        }

        if (request.SyncModel == CreateReplicaSyncModel.None) {
            throw new InvalidOperationException(
                "CreateReplicaStateAsync requires a syncable replica. SyncModel None cannot produce synchronization state.");
        }

        var submission = await client.SubmitCreateReplicaAsync(request, cancellationToken);

        CreateReplicaResult result;

        if (submission.IsPending) {
            var status = await client.WaitForCreateReplicaCompletionAsync(
                submission.StatusUrl!,
                pollingOptions,
                cancellationToken);

            if (!status.IsCompleted) {
                var message = string.IsNullOrWhiteSpace(status.ErrorMessage)
                    ? $"The createReplica job ended with status '{status.Status}'."
                    : $"The createReplica job ended with status '{status.Status}': {status.ErrorMessage}";

                throw new InvalidOperationException(message);
            }

            if (status.ResultUrl is null) {
                throw new InvalidOperationException(
                    "The createReplica job completed without a result URL.");
            }

            result = new CreateReplicaResult(
                ReplicaName: status.ReplicaName,
                ReplicaId: status.ReplicaId,
                TransportType: status.TransportType,
                ResponseType: status.ResponseType,
                SyncModel: null,
                TargetType: status.TargetType,
                ReplicaServerGen: null,
                LayerServerGens: [],
                ResultUrl: status.ResultUrl,
                Status: status.Status,
                SubmissionTime: status.SubmissionTime,
                LastUpdatedTime: status.LastUpdatedTime);
        }
        else {
            result = submission.Result
                ?? throw new InvalidOperationException(
                    "The createReplica request did not return a result.");
        }

        if (result.ResultUrl is null) {
            throw new InvalidOperationException(
                "The createReplica request completed without a result URL.");
        }

        var file = await client.DownloadCreateReplicaFileAsync(
            result.ResultUrl,
            cancellationToken);

        var stateResult = ResolveCreateReplicaStateResult(
            request,
            result,
            file);

        var state = stateResult.ToSynchronizationState(request.SyncModel);

        return new CreateReplicaStateResult(
            file,
            state,
            stateResult);
    }

    private static CreateReplicaResult ResolveCreateReplicaStateResult(
        CreateReplicaRequest request,
        CreateReplicaResult result,
        CreateReplicaFileResult file) {
        if (HasRequiredGenerationValues(request, result)) {
            return result;
        }

        if (request.DataFormat != CreateReplicaDataFormat.Json) {
            throw new InvalidOperationException(
                "The createReplica response did not include generation values, and non-JSON result files cannot be parsed by this helper. Use JSON data format or build the synchronization state explicitly.");
        }

        return ReadCreateReplicaResultFromJsonFile(file, result);
    }

    private static bool HasRequiredGenerationValues(
        CreateReplicaRequest request,
        CreateReplicaResult result) {
        return request.SyncModel switch {
            CreateReplicaSyncModel.PerReplica => result.ReplicaServerGen.HasValue,
            CreateReplicaSyncModel.PerLayer => result.LayerServerGens is { Count: > 0 },
            CreateReplicaSyncModel.None => false,
            _ => false
        };
    }

    private static CreateReplicaResult ReadCreateReplicaResultFromJsonFile(
        CreateReplicaFileResult file,
        CreateReplicaResult fallbackResult) {
        if (file.Content.Length == 0) {
            throw new InvalidOperationException(
                "The downloaded createReplica JSON file is empty.");
        }

        using var document = ParseCreateReplicaJsonFile(file);
        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Object) {
            throw new InvalidOperationException(
                "The downloaded createReplica JSON file must contain a JSON object.");
        }

        var layerServerGens = ReadLayerServerGens(root, file.ResultUrl);

        return new CreateReplicaResult(
            ReplicaName: ReadOptionalString(root, "replicaName") ?? fallbackResult.ReplicaName,
            ReplicaId: ReadOptionalString(root, "replicaID") ?? fallbackResult.ReplicaId,
            TransportType: ReadOptionalString(root, "transportType") ?? fallbackResult.TransportType,
            ResponseType: ReadOptionalString(root, "responseType") ?? fallbackResult.ResponseType,
            SyncModel: ReadOptionalString(root, "syncModel") ?? fallbackResult.SyncModel,
            TargetType: ReadOptionalString(root, "targetType") ?? fallbackResult.TargetType,
            ReplicaServerGen: ReadOptionalInt64(root, "replicaServerGen", file.ResultUrl) ??
                              fallbackResult.ReplicaServerGen,
            LayerServerGens: layerServerGens.Count > 0
                ? layerServerGens
                : fallbackResult.LayerServerGens,
            ResultUrl: fallbackResult.ResultUrl,
            Status: fallbackResult.Status,
            SubmissionTime: fallbackResult.SubmissionTime,
            LastUpdatedTime: fallbackResult.LastUpdatedTime);
    }

    private static JsonDocument ParseCreateReplicaJsonFile(
        CreateReplicaFileResult file) {
        try {
            return JsonDocument.Parse(file.Content);
        }
        catch (JsonException exception) {
            throw new InvalidOperationException(
                "The downloaded createReplica JSON file could not be parsed.",
                exception);
        }
    }

    private static string? ReadOptionalString(
        JsonElement root,
        string propertyName) {
        if (!root.TryGetProperty(propertyName, out var property) ||
            property.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) {
            return null;
        }

        if (property.ValueKind != JsonValueKind.String) {
            return null;
        }

        var value = property.GetString();

        return string.IsNullOrWhiteSpace(value)
            ? null
            : value;
    }

    private static long? ReadOptionalInt64(
        JsonElement root,
        string propertyName,
        Uri resultUrl) {
        if (!root.TryGetProperty(propertyName, out var property) ||
            property.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) {
            return null;
        }

        if (!TryReadInt64(property, out var value)) {
            throw new InvalidOperationException(
                $"The downloaded createReplica JSON file contains an invalid {propertyName} value. Result URL: {resultUrl}");
        }

        if (value < 0) {
            throw new InvalidOperationException(
                $"The downloaded createReplica JSON file contains a negative {propertyName} value. Result URL: {resultUrl}");
        }

        return value;
    }

    private static IReadOnlyList<CreateReplicaLayerServerGen> ReadLayerServerGens(
        JsonElement root,
        Uri resultUrl) {
        if (!root.TryGetProperty("layerServerGens", out var property) ||
            property.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) {
            return Array.Empty<CreateReplicaLayerServerGen>();
        }

        if (property.ValueKind != JsonValueKind.Array) {
            throw new InvalidOperationException(
                $"The downloaded createReplica JSON file contains an invalid layerServerGens value. Result URL: {resultUrl}");
        }

        var values = new List<CreateReplicaLayerServerGen>();

        foreach (var item in property.EnumerateArray()) {
            if (item.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) {
                continue;
            }

            if (item.ValueKind != JsonValueKind.Object) {
                throw new InvalidOperationException(
                    $"The downloaded createReplica JSON file contains an invalid layerServerGens item. Result URL: {resultUrl}");
            }

            var id = ReadRequiredInt32(item, "id", "layerServerGens", resultUrl);
            var serverGen = ReadRequiredInt64(item, "serverGen", "layerServerGens", resultUrl);

            if (id < 0) {
                throw new InvalidOperationException(
                    $"The downloaded createReplica JSON file contains a negative layer ID. Result URL: {resultUrl}");
            }

            if (serverGen < 0) {
                throw new InvalidOperationException(
                    $"The downloaded createReplica JSON file contains a negative serverGen value. Result URL: {resultUrl}");
            }

            values.Add(new CreateReplicaLayerServerGen(id, serverGen));
        }

        return values;
    }

    private static int ReadRequiredInt32(
        JsonElement root,
        string propertyName,
        string containerName,
        Uri resultUrl) {
        var value = ReadRequiredInt64(root, propertyName, containerName, resultUrl);

        if (value < int.MinValue || value > int.MaxValue) {
            throw new InvalidOperationException(
                $"The downloaded createReplica JSON file contains an out-of-range {propertyName} value in {containerName}. Result URL: {resultUrl}");
        }

        return (int)value;
    }

    private static long ReadRequiredInt64(
        JsonElement root,
        string propertyName,
        string containerName,
        Uri resultUrl) {
        if (!root.TryGetProperty(propertyName, out var property) ||
            property.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined ||
            !TryReadInt64(property, out var value)) {
            throw new InvalidOperationException(
                $"The downloaded createReplica JSON file contains a {containerName} item without a valid {propertyName} value. Result URL: {resultUrl}");
        }

        return value;
    }

    private static bool TryReadInt64(
        JsonElement element,
        out long value) {
        value = 0;

        return element.ValueKind switch {
            JsonValueKind.Number => element.TryGetInt64(out value),
            JsonValueKind.String => long.TryParse(
                element.GetString(),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out value),
            _ => false
        };
    }
}