using S100Framework.REST.Abstractions;
using S100Framework.REST.Models;

namespace S100Framework.REST.Extensions;

/// <summary>
/// Provides convenience helpers for asynchronous <c>synchronizeReplica</c> workflows.
/// </summary>
public static class FeatureServiceClientSynchronizeReplicaExtensions
{
    /// <summary>
    /// Polls an asynchronous <c>synchronizeReplica</c> job until it reaches a terminal state.
    /// </summary>
    /// <param name="client">
    /// The feature service client.
    /// </param>
    /// <param name="statusUrl">
    /// The status URL returned from <c>SubmitSynchronizeReplicaAsync</c>.
    /// </param>
    /// <param name="options">
    /// Polling options. When <see langword="null" />, default polling behavior is used.
    /// </param>
    /// <param name="cancellationToken">
    /// The cancellation token.
    /// </param>
    /// <returns>
    /// The final terminal job status.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="client" /> or <paramref name="statusUrl" /> is <see langword="null" />.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <paramref name="options" /> is invalid.
    /// </exception>
    /// <exception cref="TimeoutException">
    /// Thrown when the job does not complete within the configured timeout.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// Thrown when the caller cancels the operation.
    /// </exception>
    public static async Task<SynchronizeReplicaJobStatus> WaitForSynchronizeReplicaCompletionAsync(
        this IFeatureServiceClient client,
        Uri statusUrl,
        ReplicaPollingOptions? options = null,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(statusUrl);

        options ??= new ReplicaPollingOptions();
        options.Validate();

        var startedAt = DateTimeOffset.UtcNow;

        while (true) {
            cancellationToken.ThrowIfCancellationRequested();

            var status = await client.GetSynchronizeReplicaStatusAsync(statusUrl, cancellationToken);

            if (status.IsTerminal) {
                return status;
            }

            if (DateTimeOffset.UtcNow - startedAt >= options.Timeout) {
                throw new TimeoutException(
                    $"The synchronizeReplica job did not complete within {options.Timeout}.");
            }

            await Task.Delay(options.PollInterval, cancellationToken);
        }
    }

    /// <summary>
    /// Submits a URL-based <c>synchronizeReplica</c> request, waits for completion when needed,
    /// and downloads the resulting file.
    /// </summary>
    /// <param name="client">
    /// The feature service client.
    /// </param>
    /// <param name="request">
    /// The <c>synchronizeReplica</c> request.
    /// </param>
    /// <param name="options">
    /// Polling options. When <see langword="null" />, default polling behavior is used.
    /// </param>
    /// <param name="cancellationToken">
    /// The cancellation token.
    /// </param>
    /// <returns>
    /// The downloaded result file.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="client" /> or <paramref name="request" /> is <see langword="null" />.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the request does not use URL transport, when the job ends in a non-completed terminal
    /// state, or when the completed result does not expose a result URL.
    /// </exception>
    /// <exception cref="TimeoutException">
    /// Thrown when the job does not complete within the configured timeout.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// Thrown when the caller cancels the operation.
    /// </exception>
    public static async Task<SynchronizeReplicaFileResult> SubmitAndDownloadSynchronizeReplicaFileAsync(
        this IFeatureServiceClient client,
        SynchronizeReplicaRequest request,
        ReplicaPollingOptions? options = null,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(request);

        if (request.TransportType != SynchronizeReplicaTransportType.Url) {
            throw new InvalidOperationException(
                "SubmitAndDownloadSynchronizeReplicaFileAsync requires TransportType.Url.");
        }

        var submission = await client.SubmitSynchronizeReplicaAsync(request, cancellationToken);

        if (submission.IsPending) {
            var status = await client.WaitForSynchronizeReplicaCompletionAsync(
                submission.StatusUrl!,
                options,
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

            return await client.DownloadSynchronizeReplicaFileAsync(status.ResultUrl, cancellationToken);
        }

        if (submission.Result?.ResultUrl is null) {
            throw new InvalidOperationException(
                "The synchronizeReplica request completed without a result URL.");
        }

        return await client.DownloadSynchronizeReplicaFileAsync(
            submission.Result.ResultUrl,
            cancellationToken);
    }
}