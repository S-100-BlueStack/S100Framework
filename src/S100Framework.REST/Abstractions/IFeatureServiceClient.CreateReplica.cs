using S100Framework.REST.Models;

namespace S100Framework.REST.Abstractions;

/// <summary>
/// Defines <c>createReplica</c> operations for a feature service endpoint.
/// </summary>
public partial interface IFeatureServiceClient
{
    /// <summary>
    /// Sends a <c>createReplica</c> request and waits for the server to return the final result payload.
    /// </summary>
    /// <param name="request">
    /// The <c>createReplica</c> request.
    /// </param>
    /// <param name="cancellationToken">
    /// The cancellation token.
    /// </param>
    /// <returns>
    /// The final result returned by the server.
    /// </returns>
    Task<CreateReplicaResult> CreateReplicaAsync(
        CreateReplicaRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Submits a <c>createReplica</c> request.
    /// </summary>
    /// <param name="request">
    /// The <c>createReplica</c> request.
    /// </param>
    /// <param name="cancellationToken">
    /// The cancellation token.
    /// </param>
    /// <returns>
    /// A submission response that contains either an immediate result or a status URL that can be polled.
    /// </returns>
    Task<CreateReplicaSubmissionResult> SubmitCreateReplicaAsync(
        CreateReplicaRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current status of a previously submitted <c>createReplica</c> job.
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
    Task<CreateReplicaJobStatus> GetCreateReplicaStatusAsync(
        Uri statusUrl,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads a file produced by a <c>createReplica</c> result URL.
    /// </summary>
    /// <param name="resultUrl">
    /// The result URL returned directly or by the status resource.
    /// </param>
    /// <param name="cancellationToken">
    /// The cancellation token.
    /// </param>
    /// <returns>
    /// The downloaded file result.
    /// </returns>
    Task<CreateReplicaFileResult> DownloadCreateReplicaFileAsync(
        Uri resultUrl,
        CancellationToken cancellationToken = default);
}