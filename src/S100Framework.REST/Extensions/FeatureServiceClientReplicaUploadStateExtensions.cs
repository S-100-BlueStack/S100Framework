using S100Framework.REST.Abstractions;
using S100Framework.REST.Exceptions;
using S100Framework.REST.Models;

namespace S100Framework.REST.Extensions;

/// <summary>
/// Provides state-aware helpers for upload-only replica synchronization workflows.
/// </summary>
public static class FeatureServiceClientReplicaUploadStateExtensions
{
    /// <summary>
    /// Runs an upload-only replica synchronization from persisted state, uploads local edits,
    /// downloads the JSON result file, and returns the original synchronization state.
    /// </summary>
    /// <param name="client">
    /// The feature service client.
    /// </param>
    /// <param name="state">
    /// The persisted replica synchronization state.
    /// </param>
    /// <param name="request">
    /// The upload-only synchronization request options.
    /// </param>
    /// <param name="cancellationToken">
    /// The cancellation token.
    /// </param>
    /// <returns>
    /// The downloaded result file, parsed JSON result, and unchanged synchronization state.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="client" />, <paramref name="state" />, or <paramref name="request" /> is <see langword="null" />.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the state or request is invalid, when the job fails, or when the server does not return a result URL.
    /// </exception>
    /// <exception cref="ReplicaEditResultsException">
    /// Thrown when <see cref="SynchronizeReplicaStateUploadRequest.ThrowOnEditErrors" /> is enabled and the JSON result file contains failed edit results.
    /// </exception>
    /// <exception cref="TimeoutException">
    /// Thrown when an asynchronous job does not complete within the configured timeout.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// Thrown when the caller cancels the operation.
    /// </exception>
    public static async Task<SynchronizeReplicaUploadStateResult> SynchronizeReplicaStateUploadAsync(
        this IFeatureServiceClient client,
        ReplicaSynchronizationState state,
        SynchronizeReplicaStateUploadRequest request,
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

        return new SynchronizeReplicaUploadStateResult(
            file,
            jsonResult,
            state,
            synchronizationResult);
    }

    private static SynchronizeReplicaRequest BuildSynchronizeRequest(
    ReplicaSynchronizationState state,
    SynchronizeReplicaStateUploadRequest request) {
        return state.ToUploadRequest(request);
    }
}