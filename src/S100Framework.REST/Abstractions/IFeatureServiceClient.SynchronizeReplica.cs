using S100Framework.REST.Models;

namespace S100Framework.REST.Abstractions;

/// <summary>
/// Defines service-level <c>synchronizeReplica</c> operations for a feature service endpoint.
/// </summary>
public partial interface IFeatureServiceClient
{
    /// <summary>
    /// Sends a <c>synchronizeReplica</c> request and expects the server to return the final result payload immediately.
    /// </summary>
    /// <param name="request">
    /// The synchronize replica request.
    /// </param>
    /// <param name="cancellationToken">
    /// The cancellation token.
    /// </param>
    /// <returns>
    /// The final result returned by the server.
    /// </returns>
    Task<SynchronizeReplicaResult> SynchronizeReplicaAsync(
        SynchronizeReplicaRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Submits a <c>synchronizeReplica</c> request.
    /// </summary>
    /// <param name="request">
    /// The synchronize replica request.
    /// </param>
    /// <param name="cancellationToken">
    /// The cancellation token.
    /// </param>
    /// <returns>
    /// A submission response that contains either an immediate result or a status URL that can be polled.
    /// </returns>
    Task<SynchronizeReplicaSubmissionResult> SubmitSynchronizeReplicaAsync(
        SynchronizeReplicaRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current status of a previously submitted <c>synchronizeReplica</c> job.
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
    Task<SynchronizeReplicaJobStatus> GetSynchronizeReplicaStatusAsync(
        Uri statusUrl,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads a file produced by a <c>synchronizeReplica</c> result URL.
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
    Task<SynchronizeReplicaFileResult> DownloadSynchronizeReplicaFileAsync(
        Uri resultUrl,
        CancellationToken cancellationToken = default);
}