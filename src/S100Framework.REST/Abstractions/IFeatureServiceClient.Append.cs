using S100Framework.REST.Models;

namespace S100Framework.REST.Abstractions;

/// <summary>
/// Defines append-related operations for a feature service root endpoint.
/// </summary>
public partial interface IFeatureServiceClient
{
    /// <summary>
    /// Submits a service-level <c>append</c> request using the literal <c>edits</c> source.
    /// </summary>
    /// <param name="request">
    /// The append request to submit.
    /// </param>
    /// <param name="cancellationToken">
    /// The cancellation token.
    /// </param>
    /// <returns>
    /// The append submission response.
    /// </returns>
    Task<FeatureServiceAppendSubmissionResult> SubmitAppendAsync(
        FeatureServiceAppendEditsRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current status of a previously submitted <c>append</c> job.
    /// </summary>
    /// <param name="statusUrl">
    /// The append job status URL.
    /// </param>
    /// <param name="cancellationToken">
    /// The cancellation token.
    /// </param>
    /// <returns>
    /// The current append job status.
    /// </returns>
    Task<FeatureServiceAppendJobStatus> GetAppendStatusAsync(
        Uri statusUrl,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Submits a service-level <c>append</c> request and waits until the job reaches a terminal state.
    /// </summary>
    /// <param name="request">
    /// The append request to submit.
    /// </param>
    /// <param name="options">
    /// Polling options. When <see langword="null"/>, default polling behavior is used.
    /// </param>
    /// <param name="cancellationToken">
    /// The cancellation token.
    /// </param>
    /// <returns>
    /// The terminal append job status.
    /// </returns>
    Task<FeatureServiceAppendJobStatus> WaitForAppendCompletionAsync(
        FeatureServiceAppendEditsRequest request,
        AppendWaitOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Polls an existing <c>append</c> job until it reaches a terminal state.
    /// </summary>
    /// <param name="statusUrl">
    /// The append job status URL.
    /// </param>
    /// <param name="options">
    /// Polling options. When <see langword="null"/>, default polling behavior is used.
    /// </param>
    /// <param name="cancellationToken">
    /// The cancellation token.
    /// </param>
    /// <returns>
    /// The terminal append job status.
    /// </returns>
    Task<FeatureServiceAppendJobStatus> WaitForAppendCompletionAsync(
        Uri statusUrl,
        AppendWaitOptions? options = null,
        CancellationToken cancellationToken = default);
}