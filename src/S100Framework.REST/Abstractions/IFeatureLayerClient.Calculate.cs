using S100Framework.REST.Models;

namespace S100Framework.REST.Abstractions;

/// <summary>
/// Defines optional calculate operations for a feature layer endpoint.
/// </summary>
public partial interface IFeatureLayerClient
{
    /// <summary>
    /// Executes a synchronous layer-level <c>calculate</c> request for the current layer or table.
    /// </summary>
    /// <param name="request">
    /// The calculate request.
    /// </param>
    /// <param name="cancellationToken">
    /// The cancellation token.
    /// </param>
    /// <returns>
    /// The calculate result returned by the service.
    /// </returns>
    /// <exception cref="NotSupportedException">
    /// Thrown when the concrete layer client implementation does not support calculate operations.
    /// </exception>
    Task<CalculateResult> CalculateAsync(
        CalculateRequest request,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This feature layer client does not support calculate operations.");

    /// <summary>
    /// Submits an asynchronous layer-level <c>calculate</c> request for the current layer or table.
    /// </summary>
    /// <param name="request">
    /// The calculate request.
    /// </param>
    /// <param name="cancellationToken">
    /// The cancellation token.
    /// </param>
    /// <returns>
    /// A submission response that contains either an immediate result or a status URL that can be polled.
    /// </returns>
    /// <exception cref="NotSupportedException">
    /// Thrown when the concrete layer client implementation does not support async calculate operations.
    /// </exception>
    Task<CalculateSubmissionResult> SubmitCalculateAsync(
        CalculateRequest request,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This feature layer client does not support async calculate operations.");

    /// <summary>
    /// Gets the current status of a previously submitted asynchronous <c>calculate</c> job.
    /// </summary>
    /// <param name="statusUrl">
    /// The status URL returned by the submission call.
    /// </param>
    /// <param name="cancellationToken">
    /// The cancellation token.
    /// </param>
    /// <returns>
    /// The current calculate job status.
    /// </returns>
    /// <exception cref="NotSupportedException">
    /// Thrown when the concrete layer client implementation does not support async calculate operations.
    /// </exception>
    Task<CalculateJobStatus> GetCalculateStatusAsync(
        Uri statusUrl,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This feature layer client does not support async calculate operations.");

    /// <summary>
    /// Submits an asynchronous <c>calculate</c> request and waits until the job completes.
    /// </summary>
    /// <param name="request">
    /// The calculate request.
    /// </param>
    /// <param name="options">
    /// Polling options. When <see langword="null" />, default polling behavior is used.
    /// </param>
    /// <param name="cancellationToken">
    /// The cancellation token.
    /// </param>
    /// <returns>
    /// The completed calculate result.
    /// </returns>
    /// <exception cref="NotSupportedException">
    /// Thrown when the concrete layer client implementation does not support async calculate operations.
    /// </exception>
    Task<CalculateResult> WaitForCalculateCompletionAsync(
        CalculateRequest request,
        CalculateWaitOptions? options = null,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This feature layer client does not support async calculate operations.");

    /// <summary>
    /// Polls an existing asynchronous <c>calculate</c> job until it completes.
    /// </summary>
    /// <param name="statusUrl">
    /// The status URL returned by the submission call.
    /// </param>
    /// <param name="options">
    /// Polling options. When <see langword="null" />, default polling behavior is used.
    /// </param>
    /// <param name="cancellationToken">
    /// The cancellation token.
    /// </param>
    /// <returns>
    /// The completed calculate result.
    /// </returns>
    /// <exception cref="NotSupportedException">
    /// Thrown when the concrete layer client implementation does not support async calculate operations.
    /// </exception>
    Task<CalculateResult> WaitForCalculateCompletionAsync(
        Uri statusUrl,
        CalculateWaitOptions? options = null,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This feature layer client does not support async calculate operations.");
}