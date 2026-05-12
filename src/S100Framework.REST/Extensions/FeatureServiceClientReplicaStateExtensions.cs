using S100Framework.REST.Abstractions;
using S100Framework.REST.Internal.Replica;
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

        if (closeReplica) {
            throw new InvalidOperationException(
                "SynchronizeReplicaStateAsync cannot close the replica because it returns synchronization state intended for future use. Use SubmitSynchronizeReplicaAsync directly when closing a replica.");
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
        var parsed = ReplicaGenerationJsonReader.Read(
            file.Content,
            file.ResultUrl,
            "synchronizeReplica");

        return new SynchronizeReplicaResult(
            ReplicaId: parsed.ReplicaId ?? fallbackResult.ReplicaId,
            ReplicaName: parsed.ReplicaName ?? fallbackResult.ReplicaName,
            TransportType: parsed.TransportType ?? fallbackResult.TransportType,
            ResponseType: parsed.ResponseType ?? fallbackResult.ResponseType,
            ReplicaServerGen: parsed.ReplicaServerGen ?? fallbackResult.ReplicaServerGen,
            LayerServerGens: parsed.LayerServerGens.Count > 0
                ? parsed.LayerServerGens
                    .Select(static value => new SynchronizeReplicaLayerServerGen(value.Id, value.ServerGen))
                    .ToArray()
                : fallbackResult.LayerServerGens,
            ResultUrl: fallbackResult.ResultUrl,
            Status: fallbackResult.Status,
            SubmissionTime: fallbackResult.SubmissionTime,
            LastUpdatedTime: fallbackResult.LastUpdatedTime);
    }
}