using S100Framework.REST.Abstractions;
using S100Framework.REST.Models;

namespace S100Framework.REST.Extensions;

/// <summary>
/// Provides convenience helpers for asynchronous <c>createReplica</c> workflows.
/// </summary>
public static class FeatureServiceClientCreateReplicaExtensions
{
    /// <summary>
    /// Polls an asynchronous <c>createReplica</c> job until it reaches a terminal state.
    /// </summary>
    /// <param name="client">
    /// The feature service client.
    /// </param>
    /// <param name="statusUrl">
    /// The status URL returned from <c>SubmitCreateReplicaAsync</c>.
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
    public static async Task<CreateReplicaJobStatus> WaitForCreateReplicaCompletionAsync(
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

            var status = await client.GetCreateReplicaStatusAsync(statusUrl, cancellationToken);

            if (status.IsTerminal) {
                return status;
            }

            if (DateTimeOffset.UtcNow - startedAt >= options.Timeout) {
                throw new TimeoutException(
                    $"The createReplica job did not complete within {options.Timeout}.");
            }

            await Task.Delay(options.PollInterval, cancellationToken);
        }
    }

    /// <summary>
    /// Submits a URL-based <c>createReplica</c> request, waits for completion when needed,
    /// and downloads the resulting file.
    /// </summary>
    /// <param name="client">
    /// The feature service client.
    /// </param>
    /// <param name="request">
    /// The <c>createReplica</c> request.
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
    public static async Task<CreateReplicaFileResult> SubmitAndDownloadCreateReplicaFileAsync(
        this IFeatureServiceClient client,
        CreateReplicaRequest request,
        ReplicaPollingOptions? options = null,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(request);

        if (request.TransportType != CreateReplicaTransportType.Url) {
            throw new InvalidOperationException(
                "SubmitAndDownloadCreateReplicaFileAsync requires TransportType.Url.");
        }

        var submission = await client.SubmitCreateReplicaAsync(request, cancellationToken);

        if (submission.IsPending) {
            var status = await client.WaitForCreateReplicaCompletionAsync(
                submission.StatusUrl!,
                options,
                cancellationToken);

            if (!status.IsCompleted) {
                throw new InvalidOperationException(
                    $"The createReplica job ended with status '{status.Status}'.");
            }

            if (status.ResultUrl is null) {
                throw new InvalidOperationException(
                    "The createReplica job completed without a result URL.");
            }

            return await client.DownloadCreateReplicaFileAsync(status.ResultUrl, cancellationToken);
        }

        if (submission.Result?.ResultUrl is null) {
            throw new InvalidOperationException(
                "The createReplica request completed without a result URL.");
        }

        return await client.DownloadCreateReplicaFileAsync(
            submission.Result.ResultUrl,
            cancellationToken);
    }
}