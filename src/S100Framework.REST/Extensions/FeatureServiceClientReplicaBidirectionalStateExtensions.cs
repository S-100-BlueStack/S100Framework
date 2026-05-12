using S100Framework.REST.Abstractions;
using S100Framework.REST.Models;
using S100Framework.REST.Exceptions;

namespace S100Framework.REST.Extensions;

/// <summary>
/// Provides state-aware helpers for bidirectional replica synchronization workflows.
/// </summary>
public static class FeatureServiceClientReplicaBidirectionalStateExtensions
{
    /// <summary>
    /// Runs a bidirectional replica synchronization from persisted state, uploads local edits,
    /// downloads the JSON result file, and returns the updated synchronization state.
    /// </summary>
    /// <param name="client">
    /// The feature service client.
    /// </param>
    /// <param name="state">
    /// The persisted replica synchronization state.
    /// </param>
    /// <param name="request">
    /// The bidirectional synchronization request options.
    /// </param>
    /// <param name="cancellationToken">
    /// The cancellation token.
    /// </param>
    /// <returns>
    /// The downloaded result file, parsed JSON result, and updated synchronization state.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="client" />, <paramref name="state" />, or <paramref name="request" /> is <see langword="null" />.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the state or request is invalid, when the job fails, or when the JSON result file cannot update state.
    /// </exception>
    /// <exception cref="ReplicaEditResultsException">
    /// Thrown when <see cref="SynchronizeReplicaStateBidirectionalRequest.ThrowOnEditErrors" /> is enabled and the JSON result file contains failed edit results.
    /// </exception>
    /// <exception cref="TimeoutException">
    /// Thrown when an asynchronous job does not complete within the configured timeout.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// Thrown when the caller cancels the operation.
    /// </exception>
    public static async Task<SynchronizeReplicaBidirectionalStateResult> SynchronizeReplicaStateBidirectionalAsync(
        this IFeatureServiceClient client,
        ReplicaSynchronizationState state,
        SynchronizeReplicaStateBidirectionalRequest request,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(request);

        state.Validate();
        request.Validate();

        var synchronizeRequest = BuildSynchronizeRequest(state, request);
        var submission = await client.SubmitSynchronizeReplicaAsync(synchronizeRequest, cancellationToken);

        SynchronizeReplicaResult synchronizationResult;

        if (submission.IsPending) {
            var status = await client.WaitForSynchronizeReplicaCompletionAsync(
                submission.StatusUrl!,
                request.PollingOptions,
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

            synchronizationResult = new SynchronizeReplicaResult(
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
            synchronizationResult = submission.Result
                ?? throw new InvalidOperationException(
                    "The synchronizeReplica request did not return a result.");
        }

        if (synchronizationResult.ResultUrl is null) {
            throw new InvalidOperationException(
                "The synchronizeReplica request completed without a result URL.");
        }

        var file = await client.DownloadSynchronizeReplicaFileAsync(
            synchronizationResult.ResultUrl,
            cancellationToken);

        var jsonResult = file.ReadJsonResultFile();

        if (request.ThrowOnEditErrors) {
            jsonResult.ThrowIfEditErrors();
        }

        var stateUpdateResult = MergeSynchronizationResult(
            synchronizationResult,
            jsonResult);

        var updatedState = state.UpdateFrom(stateUpdateResult);

        return new SynchronizeReplicaBidirectionalStateResult(
            file,
            jsonResult,
            updatedState,
            stateUpdateResult);
    }

    private static SynchronizeReplicaRequest BuildSynchronizeRequest(
        ReplicaSynchronizationState state,
        SynchronizeReplicaStateBidirectionalRequest request) {
        var editsJson = request.Edits is null
            ? request.EditsJson
            : request.Edits.ToJson(request.EditsJsonOptions);

        return state.SyncModel switch {
            SynchronizeReplicaSyncModel.PerReplica => new SynchronizeReplicaRequest {
                ReplicaId = state.ReplicaId,
                SyncModel = SynchronizeReplicaSyncModel.PerReplica,
                ReplicaServerGen = state.ReplicaServerGen,
                SyncDirection = SynchronizeReplicaSyncDirection.Bidirectional,
                TransportType = SynchronizeReplicaTransportType.Url,
                DataFormat = SynchronizeReplicaDataFormat.Json,
                IsAsync = request.IsAsync,
                ReturnAttachmentsDataByUrl = request.ReturnAttachmentsDataByUrl,
                RollbackOnFailure = request.RollbackOnFailure,
                ReturnIdsForAdds = request.ReturnIdsForAdds,
                EditsJson = editsJson,
                EditsUploadId = request.EditsUploadId
            },
            SynchronizeReplicaSyncModel.PerLayer => new SynchronizeReplicaRequest {
                ReplicaId = state.ReplicaId,
                SyncModel = SynchronizeReplicaSyncModel.PerLayer,
                SyncLayers = state.LayerServerGens
                    .Select(static value => new SynchronizeReplicaSyncLayer {
                        Id = value.Id,
                        ServerGen = value.ServerGen,
                        SyncDirection = SynchronizeReplicaSyncDirection.Bidirectional
                    })
                    .ToArray(),
                SyncDirection = SynchronizeReplicaSyncDirection.Bidirectional,
                TransportType = SynchronizeReplicaTransportType.Url,
                DataFormat = SynchronizeReplicaDataFormat.Json,
                IsAsync = request.IsAsync,
                ReturnAttachmentsDataByUrl = request.ReturnAttachmentsDataByUrl,
                RollbackOnFailure = request.RollbackOnFailure,
                ReturnIdsForAdds = request.ReturnIdsForAdds,
                EditsJson = editsJson,
                EditsUploadId = request.EditsUploadId
            },
            _ => throw new ArgumentOutOfRangeException(nameof(state), state.SyncModel, null)
        };
    }

    private static SynchronizeReplicaResult MergeSynchronizationResult(
        SynchronizeReplicaResult fallbackResult,
        ReplicaJsonResultFile jsonResult) {
        return new SynchronizeReplicaResult(
            ReplicaId: jsonResult.ReplicaId ?? fallbackResult.ReplicaId,
            ReplicaName: jsonResult.ReplicaName ?? fallbackResult.ReplicaName,
            TransportType: jsonResult.TransportType ?? fallbackResult.TransportType,
            ResponseType: jsonResult.ResponseType ?? fallbackResult.ResponseType,
            ReplicaServerGen: jsonResult.ReplicaServerGen ?? fallbackResult.ReplicaServerGen,
            LayerServerGens: jsonResult.LayerServerGens.Count > 0
                ? jsonResult.LayerServerGens
                    .Select(static value => new SynchronizeReplicaLayerServerGen(value.Id, value.ServerGen))
                    .ToArray()
                : fallbackResult.LayerServerGens,
            ResultUrl: fallbackResult.ResultUrl,
            Status: fallbackResult.Status,
            SubmissionTime: fallbackResult.SubmissionTime,
            LastUpdatedTime: fallbackResult.LastUpdatedTime);
    }
}