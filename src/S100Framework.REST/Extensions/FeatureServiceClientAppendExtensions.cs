using S100Framework.REST.Abstractions;
using S100Framework.REST.Models;

namespace S100Framework.REST.Extensions;

/// <summary>
/// Provides convenience helpers for asynchronous append workflows.
/// </summary>
public static class FeatureServiceClientAppendExtensions
{
    /// <summary>
    /// Submits an append request, waits for completion, and returns the final append job status.
    /// </summary>
    /// <param name="client">
    /// The feature service client.
    /// </param>
    /// <param name="request">
    /// The append request to submit.
    /// </param>
    /// <param name="options">
    /// Polling options used while waiting for the job to complete. When <see langword="null"/>,
    /// default polling behavior is used.
    /// </param>
    /// <param name="cancellationToken">
    /// The cancellation token.
    /// </param>
    /// <returns>
    /// The final completed append job status.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="client"/> or <paramref name="request"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the append job reaches a terminal state that is not completed.
    /// </exception>
    /// <exception cref="TimeoutException">
    /// Thrown when the append job does not complete within the configured timeout.
    /// </exception>
    public static async Task<FeatureServiceAppendJobStatus> SubmitAndWaitForAppendAsync(
        this IFeatureServiceClient client,
        FeatureServiceAppendEditsRequest request,
        AppendWaitOptions? options = null,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(request);

        var status = await client.WaitForAppendCompletionAsync(
            request,
            options,
            cancellationToken);

        if (!status.IsCompleted) {
            throw new InvalidOperationException(
                $"The append job ended with status '{status.Status}'.");
        }

        return status;
    }
}