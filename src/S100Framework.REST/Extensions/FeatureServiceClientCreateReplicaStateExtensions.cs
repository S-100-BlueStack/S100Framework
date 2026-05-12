using S100Framework.REST.Abstractions;
using S100Framework.REST.Models;
using S100Framework.REST.Internal.Replica;

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
        var parsed = ReplicaGenerationJsonReader.Read(
            file.Content,
            file.ResultUrl,
            "createReplica");

        return new CreateReplicaResult(
            ReplicaName: parsed.ReplicaName ?? fallbackResult.ReplicaName,
            ReplicaId: parsed.ReplicaId ?? fallbackResult.ReplicaId,
            TransportType: parsed.TransportType ?? fallbackResult.TransportType,
            ResponseType: parsed.ResponseType ?? fallbackResult.ResponseType,
            SyncModel: parsed.SyncModel ?? fallbackResult.SyncModel,
            TargetType: parsed.TargetType ?? fallbackResult.TargetType,
            ReplicaServerGen: parsed.ReplicaServerGen ?? fallbackResult.ReplicaServerGen,
            LayerServerGens: parsed.LayerServerGens.Count > 0
                ? parsed.LayerServerGens
                    .Select(static value => new CreateReplicaLayerServerGen(value.Id, value.ServerGen))
                    .ToArray()
                : fallbackResult.LayerServerGens,
            ResultUrl: fallbackResult.ResultUrl,
            Status: fallbackResult.Status,
            SubmissionTime: fallbackResult.SubmissionTime,
            LastUpdatedTime: fallbackResult.LastUpdatedTime);
    }
}