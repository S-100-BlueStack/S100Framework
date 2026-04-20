using S100Framework.REST.Abstractions;
using S100Framework.REST.Models;

namespace S100Framework.REST.Extensions;

public static class FeatureLayerClientApplyEditsExtensions
{
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