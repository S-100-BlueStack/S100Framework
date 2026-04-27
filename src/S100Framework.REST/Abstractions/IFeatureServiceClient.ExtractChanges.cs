using S100Framework.REST.Models;

namespace S100Framework.REST.Abstractions;

/// <summary>
/// Defines <c>extractChanges</c> operations for a feature service endpoint.
/// </summary>
public partial interface IFeatureServiceClient
{
    /// <summary>
    /// Sends an <c>extractChanges</c> request and waits for the server to return the final
    /// result payload.
    /// </summary>
    /// <param name="request">
    /// The <c>extractChanges</c> request.
    /// </param>
    /// <param name="cancellationToken">
    /// The cancellation token.
    /// </param>
    /// <returns>
    /// The final result returned by the server.
    /// </returns>
    Task<ExtractChangesResult> ExtractChangesAsync(
        ExtractChangesRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Submits an asynchronous <c>extractChanges</c> job.
    /// </summary>
    /// <param name="request">
    /// The <c>extractChanges</c> request.
    /// </param>
    /// <param name="cancellationToken">
    /// The cancellation token.
    /// </param>
    /// <returns>
    /// A submission response that contains either an immediate result or a status URL
    /// that can be polled.
    /// </returns>
    Task<ExtractChangesSubmissionResult> SubmitExtractChangesAsync(
        ExtractChangesRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current status of a previously submitted <c>extractChanges</c> job.
    /// </summary>
    /// <param name="statusUrl">
    /// The status URL returned from the submission call.
    /// </param>
    /// <param name="cancellationToken">
    /// The cancellation token.
    /// </param>
    /// <returns>
    /// The current job status.
    /// </returns>
    Task<ExtractChangesJobStatus> GetExtractChangesStatusAsync(
        Uri statusUrl,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads a file produced by an asynchronous <c>extractChanges</c> job.
    /// </summary>
    /// <param name="resultUrl">
    /// The result URL returned by the status resource.
    /// </param>
    /// <param name="cancellationToken">
    /// The cancellation token.
    /// </param>
    /// <returns>
    /// The downloaded file result.
    /// </returns>
    Task<ExtractChangesFileResult> DownloadExtractChangesFileAsync(
        Uri resultUrl,
        CancellationToken cancellationToken = default);
}