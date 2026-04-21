using S100Framework.REST.Abstractions;
using S100Framework.REST.Models;

namespace S100Framework.REST.Extensions;

/// <summary>
/// Provides convenience helpers for asynchronous <c>extractChanges</c> workflows.
/// </summary>
public static class FeatureServiceClientExtractChangesExtensions
{
    /// <summary>
    /// Polls an asynchronous <c>extractChanges</c> job until it reaches a terminal state.
    /// </summary>
    /// <param name="client">The feature service client.</param>
    /// <param name="statusUrl">The status URL returned from <c>SubmitExtractChangesAsync</c>.</param>
    /// <param name="options">
    /// Polling options. When <see langword="null"/>, default polling behavior is used.
    /// </param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The final terminal job status.</returns>
    public static async Task<ExtractChangesJobStatus> WaitForExtractChangesCompletionAsync(
        this IFeatureServiceClient client,
        Uri statusUrl,
        ExtractChangesPollingOptions? options = null,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(statusUrl);

        options ??= new ExtractChangesPollingOptions();
        options.Validate();

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(options.Timeout);

        while (true) {
            var status = await client.GetExtractChangesStatusAsync(statusUrl, timeoutCts.Token);

            if (status.IsTerminal) {
                return status;
            }

            await Task.Delay(options.PollInterval, timeoutCts.Token);
        }
    }

    /// <summary>
    /// Submits an asynchronous SQLite <c>extractChanges</c> job, waits for completion, and downloads the result file.
    /// </summary>
    /// <param name="client">The feature service client.</param>
    /// <param name="request">The <c>extractChanges</c> request.</param>
    /// <param name="options">
    /// Polling options. When <see langword="null"/>, default polling behavior is used.
    /// </param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The downloaded result file.</returns>
    public static async Task<ExtractChangesFileResult> SubmitAndDownloadExtractChangesFileAsync(
        this IFeatureServiceClient client,
        ExtractChangesRequest request,
        ExtractChangesPollingOptions? options = null,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(request);

        if (request.DataFormat != ExtractChangesDataFormat.Sqlite) {
            throw new InvalidOperationException(
                "SubmitAndDownloadExtractChangesFileAsync requires DataFormat.Sqlite.");
        }

        var submission = await client.SubmitExtractChangesAsync(request, cancellationToken);

        if (!submission.IsPending || submission.StatusUrl is null) {
            throw new InvalidOperationException(
                "The extractChanges request did not return a pending async job with a status URL.");
        }

        var status = await client.WaitForExtractChangesCompletionAsync(
            submission.StatusUrl,
            options,
            cancellationToken);

        if (!status.IsCompleted) {
            throw new InvalidOperationException(
                $"The extractChanges job ended with status '{status.Status}'.");
        }

        if (status.ResultUrl is null) {
            throw new InvalidOperationException(
                "The extractChanges job completed without a result URL.");
        }

        return await client.DownloadExtractChangesFileAsync(status.ResultUrl, cancellationToken);
    }
}