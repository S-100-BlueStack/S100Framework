using S100Framework.REST.Abstractions;
using S100Framework.REST.Models;

namespace S100Framework.REST.Extensions;

/// <summary>
/// Provides convenience helpers for asynchronous layer-level <c>applyEdits</c> workflows.
/// </summary>
public static class FeatureLayerClientApplyEditsExtensions
{
    /// <summary>
    /// Polls an asynchronous layer-level <c>applyEdits</c> job until it reaches a terminal state.
    /// </summary>
    /// <param name="client">
    /// The feature layer client.
    /// </param>
    /// <param name="statusUrl">
    /// The status URL returned from <c>SubmitApplyEditsAsync</c>.
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
    /// Thrown when <paramref name="client" /> or <paramref name="statusUrl" /> is
    /// <see langword="null" />.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <paramref name="options" /> is invalid.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// Thrown when the caller cancels the operation or the configured timeout is reached.
    /// </exception>
    public static async Task<ApplyEditsJobStatus> WaitForApplyEditsCompletionAsync(
        this IFeatureLayerClient client,
        Uri statusUrl,
        ApplyEditsPollingOptions? options = null,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(statusUrl);

        options ??= new ApplyEditsPollingOptions();
        options.Validate();

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(options.Timeout);

        while (true) {
            var status = await client.GetApplyEditsStatusAsync(statusUrl, timeoutCts.Token);

            if (status.IsTerminal) {
                return status;
            }

            await Task.Delay(options.PollInterval, timeoutCts.Token);
        }
    }

    /// <summary>
    /// Submits a layer-level <c>applyEdits</c> request, waits for completion when the server
    /// responds asynchronously, and returns the final edit result.
    /// </summary>
    /// <param name="client">
    /// The feature layer client.
    /// </param>
    /// <param name="edits">
    /// The edits to submit.
    /// </param>
    /// <param name="options">
    /// Polling options used when the server responds asynchronously. When
    /// <see langword="null" />, default polling behavior is used.
    /// </param>
    /// <param name="cancellationToken">
    /// The cancellation token.
    /// </param>
    /// <returns>
    /// The final <c>applyEdits</c> result.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="client" /> or <paramref name="edits" /> is
    /// <see langword="null" />.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the submission result is incomplete, when the job ends in a non-completed
    /// terminal state, or when a completed job does not expose a result URL.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// Thrown when the caller cancels the operation or the polling timeout is reached.
    /// </exception>
    public static async Task<ApplyEditsResult> SubmitAndWaitForApplyEditsAsync(
        this IFeatureLayerClient client,
        FeatureEdits edits,
        ApplyEditsPollingOptions? options = null,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(edits);

        var submission = await client.SubmitApplyEditsAsync(edits, cancellationToken);

        if (submission.Result is not null) {
            return submission.Result;
        }

        if (!submission.IsPending || submission.StatusUrl is null) {
            throw new InvalidOperationException(
                "The applyEdits request did not return an embedded result or a pending async job.");
        }

        var status = await client.WaitForApplyEditsCompletionAsync(
            submission.StatusUrl,
            options,
            cancellationToken);

        if (!status.IsCompleted) {
            throw new InvalidOperationException(
                $"The applyEdits job ended with status '{status.Status}'.");
        }

        if (status.ResultUrl is null) {
            throw new InvalidOperationException(
                "The applyEdits job completed without a result URL.");
        }

        return await client.GetApplyEditsResultAsync(status.ResultUrl, cancellationToken);
    }
}