using System.Globalization;
using System.Text.Json;
using S100Framework.REST.Abstractions;
using S100Framework.REST.Models;

namespace S100Framework.REST.Extensions;

/// <summary>
/// Provides state-aware convenience helpers for replica synchronization workflows.
/// </summary>
public static class FeatureServiceClientReplicaStateExtensions
{
    /// <summary>
    /// Synchronizes a replica from persisted state, downloads the produced file, and returns the updated state.
    /// </summary>
    /// <param name="client">
    /// The feature service client.
    /// </param>
    /// <param name="state">
    /// The persisted replica synchronization state.
    /// </param>
    /// <param name="dataFormat">
    /// The requested synchronization data format.
    /// </param>
    /// <param name="transportType">
    /// The requested transport type.
    /// </param>
    /// <param name="isAsync">
    /// A value indicating whether the request should be submitted asynchronously.
    /// </param>
    /// <param name="returnAttachmentsDataByUrl">
    /// A value indicating whether attachment content should be referenced by URL instead of embedded.
    /// </param>
    /// <param name="closeReplica">
    /// A value indicating whether the replica should be closed after synchronization.
    /// </param>
    /// <param name="pollingOptions">
    /// Polling options used when the service returns an asynchronous status URL.
    /// </param>
    /// <param name="cancellationToken">
    /// The cancellation token.
    /// </param>
    /// <returns>
    /// The downloaded synchronization file and the updated synchronization state.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="client" /> or <paramref name="state" /> is <see langword="null" />.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the state is invalid, when URL transport is not used, when the job fails,
    /// or when the completed result does not expose the information required to update the state.
    /// </exception>
    /// <exception cref="TimeoutException">
    /// Thrown when an asynchronous job does not complete within the configured timeout.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// Thrown when the caller cancels the operation.
    /// </exception>
    public static async Task<SynchronizeReplicaStateResult> SynchronizeReplicaStateAsync(
        this IFeatureServiceClient client,
        ReplicaSynchronizationState state,
        SynchronizeReplicaDataFormat dataFormat = SynchronizeReplicaDataFormat.Json,
        SynchronizeReplicaTransportType transportType = SynchronizeReplicaTransportType.Url,
        bool isAsync = false,
        bool returnAttachmentsDataByUrl = false,
        bool closeReplica = false,
        ReplicaPollingOptions? pollingOptions = null,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(state);

        if (transportType != SynchronizeReplicaTransportType.Url) {
            throw new InvalidOperationException(
                "SynchronizeReplicaStateAsync requires TransportType.Url because it downloads the synchronization result file.");
        }

        var request = state.ToDownloadRequest(
            dataFormat,
            transportType,
            isAsync,
            returnAttachmentsDataByUrl,
            closeReplica);

        var submission = await client.SubmitSynchronizeReplicaAsync(request, cancellationToken);

        SynchronizeReplicaResult result;

        if (submission.IsPending) {
            var status = await client.WaitForSynchronizeReplicaCompletionAsync(
                submission.StatusUrl!,
                pollingOptions,
                cancellationToken);

            if (!status.IsCompleted) {
                var message = string.IsNullOrWhiteSpace(status.ErrorMessage)
                    ? $"The synchronizeReplica job ended with status '{status.Status}'."
                    : $"The synchronizeReplica job ended with status '{status.Status}': {status.ErrorMessage}";

                throw new InvalidOperationException(message);
            }

            if (status.ResultUrl is null) {
                throw new InvalidOperationException(
                    "The synchronizeReplica job completed without a result URL.");
            }

            result = new SynchronizeReplicaResult(
                ReplicaId: state.ReplicaId,
                ReplicaName: state.ReplicaName,
                TransportType: status.TransportType,
                ResponseType: status.ResponseType,
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
                    "The synchronizeReplica request did not return a result.");
        }

        if (result.ResultUrl is null) {
            throw new InvalidOperationException(
                "The synchronizeReplica request completed without a result URL.");
        }

        var file = await client.DownloadSynchronizeReplicaFileAsync(
            result.ResultUrl,
            cancellationToken);

        var stateUpdateResult = ResolveStateUpdateResult(
            state,
            result,
            file,
            dataFormat);

        var updatedState = state.UpdateFrom(stateUpdateResult);

        return new SynchronizeReplicaStateResult(
            file,
            updatedState,
            stateUpdateResult);
    }

    private static SynchronizeReplicaResult ResolveStateUpdateResult(
        ReplicaSynchronizationState state,
        SynchronizeReplicaResult result,
        SynchronizeReplicaFileResult file,
        SynchronizeReplicaDataFormat dataFormat) {
        if (HasRequiredGenerationValues(state, result)) {
            return result;
        }

        if (dataFormat != SynchronizeReplicaDataFormat.Json) {
            throw new InvalidOperationException(
                "The synchronizeReplica response did not include generation values, and non-JSON result files cannot be parsed by this helper. Use JSON data format or update the synchronization state explicitly.");
        }

        return ReadSynchronizationResultFromJsonFile(file, result);
    }

    private static bool HasRequiredGenerationValues(
        ReplicaSynchronizationState state,
        SynchronizeReplicaResult result) {
        return state.SyncModel switch {
            SynchronizeReplicaSyncModel.PerReplica => result.ReplicaServerGen.HasValue,
            SynchronizeReplicaSyncModel.PerLayer => result.LayerServerGens is { Count: > 0 },
            _ => false
        };
    }

    private static SynchronizeReplicaResult ReadSynchronizationResultFromJsonFile(
        SynchronizeReplicaFileResult file,
        SynchronizeReplicaResult fallbackResult) {
        if (file.Content.Length == 0) {
            throw new InvalidOperationException(
                "The downloaded synchronizeReplica JSON file is empty.");
        }

        using var document = ParseSynchronizationJsonFile(file);
        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Object) {
            throw new InvalidOperationException(
                "The downloaded synchronizeReplica JSON file must contain a JSON object.");
        }

        var layerServerGens = ReadLayerServerGens(root, file.ResultUrl);

        return new SynchronizeReplicaResult(
            ReplicaId: ReadOptionalString(root, "replicaID") ?? fallbackResult.ReplicaId,
            ReplicaName: ReadOptionalString(root, "replicaName") ?? fallbackResult.ReplicaName,
            TransportType: ReadOptionalString(root, "transportType") ?? fallbackResult.TransportType,
            ResponseType: ReadOptionalString(root, "responseType") ?? fallbackResult.ResponseType,
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

    private static JsonDocument ParseSynchronizationJsonFile(
        SynchronizeReplicaFileResult file) {
        try {
            return JsonDocument.Parse(file.Content);
        }
        catch (JsonException exception) {
            throw new InvalidOperationException(
                "The downloaded synchronizeReplica JSON file could not be parsed.",
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
                $"The downloaded synchronizeReplica JSON file contains an invalid {propertyName} value. Result URL: {resultUrl}");
        }

        if (value < 0) {
            throw new InvalidOperationException(
                $"The downloaded synchronizeReplica JSON file contains a negative {propertyName} value. Result URL: {resultUrl}");
        }

        return value;
    }

    private static IReadOnlyList<SynchronizeReplicaLayerServerGen> ReadLayerServerGens(
        JsonElement root,
        Uri resultUrl) {
        if (!root.TryGetProperty("layerServerGens", out var property) ||
            property.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) {
            return Array.Empty<SynchronizeReplicaLayerServerGen>();
        }

        if (property.ValueKind != JsonValueKind.Array) {
            throw new InvalidOperationException(
                $"The downloaded synchronizeReplica JSON file contains an invalid layerServerGens value. Result URL: {resultUrl}");
        }

        var values = new List<SynchronizeReplicaLayerServerGen>();

        foreach (var item in property.EnumerateArray()) {
            if (item.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) {
                continue;
            }

            if (item.ValueKind != JsonValueKind.Object) {
                throw new InvalidOperationException(
                    $"The downloaded synchronizeReplica JSON file contains an invalid layerServerGens item. Result URL: {resultUrl}");
            }

            var id = ReadRequiredInt32(item, "id", "layerServerGens", resultUrl);
            var serverGen = ReadRequiredInt64(item, "serverGen", "layerServerGens", resultUrl);

            if (id < 0) {
                throw new InvalidOperationException(
                    $"The downloaded synchronizeReplica JSON file contains a negative layer ID. Result URL: {resultUrl}");
            }

            if (serverGen < 0) {
                throw new InvalidOperationException(
                    $"The downloaded synchronizeReplica JSON file contains a negative serverGen value. Result URL: {resultUrl}");
            }

            values.Add(new SynchronizeReplicaLayerServerGen(id, serverGen));
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
                $"The downloaded synchronizeReplica JSON file contains an out-of-range {propertyName} value in {containerName}. Result URL: {resultUrl}");
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
                $"The downloaded synchronizeReplica JSON file contains a {containerName} item without a valid {propertyName} value. Result URL: {resultUrl}");
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