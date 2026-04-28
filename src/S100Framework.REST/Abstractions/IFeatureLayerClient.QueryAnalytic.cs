using S100Framework.REST.Models;

namespace S100Framework.REST.Abstractions;

/// <summary>
/// Defines optional analytic query operations for a feature layer endpoint.
/// </summary>
public partial interface IFeatureLayerClient
{
    /// <summary>
    /// Executes a synchronous <c>queryAnalytic</c> request for the current layer or table.
    /// </summary>
    /// <param name="request">
    /// The analytic query request.
    /// </param>
    /// <param name="cancellationToken">
    /// The cancellation token.
    /// </param>
    /// <returns>
    /// The analytic query result returned by the service.
    /// </returns>
    /// <exception cref="NotSupportedException">
    /// Thrown when the concrete layer client implementation does not support analytic query operations.
    /// </exception>
    Task<QueryAnalyticResult> QueryAnalyticAsync(
        QueryAnalyticRequest request,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This feature layer client does not support queryAnalytic operations.");

    /// <summary>
    /// Submits an asynchronous <c>queryAnalytic</c> request for the current layer or table.
    /// </summary>
    /// <param name="request">
    /// The analytic query request.
    /// </param>
    /// <param name="cancellationToken">
    /// The cancellation token.
    /// </param>
    /// <returns>
    /// A submission response that contains either an immediate result or a status URL that can be polled.
    /// </returns>
    /// <exception cref="NotSupportedException">
    /// Thrown when the concrete layer client implementation does not support async analytic query operations.
    /// </exception>
    Task<QueryAnalyticSubmissionResult> SubmitQueryAnalyticAsync(
        QueryAnalyticRequest request,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This feature layer client does not support async queryAnalytic operations.");

    /// <summary>
    /// Gets the current status of a previously submitted asynchronous <c>queryAnalytic</c> job.
    /// </summary>
    /// <param name="statusUrl">
    /// The status URL returned by the submission call.
    /// </param>
    /// <param name="cancellationToken">
    /// The cancellation token.
    /// </param>
    /// <returns>
    /// The current analytic query job status.
    /// </returns>
    /// <exception cref="NotSupportedException">
    /// Thrown when the concrete layer client implementation does not support async analytic query operations.
    /// </exception>
    Task<QueryAnalyticJobStatus> GetQueryAnalyticStatusAsync(
        Uri statusUrl,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This feature layer client does not support async queryAnalytic operations.");

    /// <summary>
    /// Gets the result payload for a completed asynchronous <c>queryAnalytic</c> job.
    /// </summary>
    /// <param name="resultUrl">
    /// The result URL returned by the status resource.
    /// </param>
    /// <param name="cancellationToken">
    /// The cancellation token.
    /// </param>
    /// <returns>
    /// The completed analytic query result.
    /// </returns>
    /// <exception cref="NotSupportedException">
    /// Thrown when the concrete layer client implementation does not support async analytic query operations.
    /// </exception>
    Task<QueryAnalyticResult> GetQueryAnalyticResultAsync(
        Uri resultUrl,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This feature layer client does not support async queryAnalytic operations.");

    /// <summary>
    /// Submits an asynchronous <c>queryAnalytic</c> request and waits until the job completes.
    /// </summary>
    /// <param name="request">
    /// The analytic query request.
    /// </param>
    /// <param name="options">
    /// Polling options. When <see langword="null" />, default polling behavior is used.
    /// </param>
    /// <param name="cancellationToken">
    /// The cancellation token.
    /// </param>
    /// <returns>
    /// The completed analytic query result.
    /// </returns>
    /// <exception cref="NotSupportedException">
    /// Thrown when the concrete layer client implementation does not support async analytic query operations.
    /// </exception>
    Task<QueryAnalyticResult> WaitForQueryAnalyticCompletionAsync(
        QueryAnalyticRequest request,
        QueryAnalyticWaitOptions? options = null,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This feature layer client does not support async queryAnalytic operations.");

    /// <summary>
    /// Polls an existing asynchronous <c>queryAnalytic</c> job until it completes.
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
    /// The completed analytic query result.
    /// </returns>
    /// <exception cref="NotSupportedException">
    /// Thrown when the concrete layer client implementation does not support async analytic query operations.
    /// </exception>
    Task<QueryAnalyticResult> WaitForQueryAnalyticCompletionAsync(
        Uri statusUrl,
        QueryAnalyticWaitOptions? options = null,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This feature layer client does not support async queryAnalytic operations.");
}