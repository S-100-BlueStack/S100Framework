using S100Framework.REST.Models;

namespace S100Framework.REST.Abstractions;

/// <summary>
/// Defines the public contract for working with a feature service root endpoint.
/// </summary>
/// <remarks>
/// Implementations provide service metadata access, layer resolution, service-level
/// edit operations, and <c>extractChanges</c> workflows.
/// </remarks>
public interface IFeatureServiceClient
{
    /// <summary>
    /// Gets metadata for the current feature service.
    /// </summary>
    /// <param name="cancellationToken">
    /// The cancellation token.
    /// </param>
    /// <returns>
    /// The service metadata.
    /// </returns>
    Task<FeatureServiceMetadata> GetMetadataAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a layer client for the specified layer ID.
    /// </summary>
    /// <param name="layerId">
    /// The layer ID.
    /// </param>
    /// <returns>
    /// A layer client bound to the specified layer.
    /// </returns>
    IFeatureLayerClient GetLayerClient(int layerId);

    /// <summary>
    /// Sends a service-level <c>applyEdits</c> request and waits for the server to return
    /// the final multi-layer edit result.
    /// </summary>
    /// <param name="edits">
    /// The multi-layer edits to apply.
    /// </param>
    /// <param name="cancellationToken">
    /// The cancellation token.
    /// </param>
    /// <returns>
    /// The final multi-layer edit result returned by the server.
    /// </returns>
    Task<FeatureServiceApplyEditsResult> ApplyEditsAsync(
        FeatureServiceEdits edits,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves a layer or table by name and creates a client for the matching dataset.
    /// </summary>
    /// <param name="layerName">
    /// The layer or table name to look up.
    /// </param>
    /// <param name="cancellationToken">
    /// The cancellation token.
    /// </param>
    /// <returns>
    /// A layer client bound to the resolved layer or table.
    /// </returns>
    Task<IFeatureLayerClient> GetLayerClientAsync(
        string layerName,
        CancellationToken cancellationToken = default);

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

    /// <summary>
    /// Submits a service-level <c>applyEdits</c> request as an asynchronous job when the
    /// server supports it.
    /// </summary>
    /// <param name="edits">
    /// The multi-layer edits to submit.
    /// </param>
    /// <param name="cancellationToken">
    /// The cancellation token.
    /// </param>
    /// <returns>
    /// A submission response that contains either an immediate result or a status URL
    /// that can be polled.
    /// </returns>
    Task<FeatureServiceApplyEditsSubmissionResult> SubmitApplyEditsAsync(
        FeatureServiceEdits edits,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current status of a previously submitted service-level <c>applyEdits</c> job.
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
    Task<ApplyEditsJobStatus> GetApplyEditsStatusAsync(
        Uri statusUrl,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the result payload for a completed service-level <c>applyEdits</c> job.
    /// </summary>
    /// <param name="resultUrl">
    /// The result URL returned by the status resource.
    /// </param>
    /// <param name="cancellationToken">
    /// The cancellation token.
    /// </param>
    /// <returns>
    /// The completed multi-layer edit result.
    /// </returns>
    Task<FeatureServiceApplyEditsResult> GetApplyEditsResultAsync(
        Uri resultUrl,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Submits a service-level <c>applyEdits</c> request and waits until the job completes.
    /// </summary>
    /// <param name="edits">
    /// The multi-layer edits to submit.
    /// </param>
    /// <param name="options">
    /// Polling options. When <see langword="null" />, default polling behavior is used.
    /// </param>
    /// <param name="cancellationToken">
    /// The cancellation token.
    /// </param>
    /// <returns>
    /// The completed multi-layer edit result.
    /// </returns>
    Task<FeatureServiceApplyEditsResult> WaitForApplyEditsCompletionAsync(
        FeatureServiceEdits edits,
        ApplyEditsWaitOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Polls an existing service-level <c>applyEdits</c> job until it completes.
    /// </summary>
    /// <param name="statusUrl">
    /// The status URL returned from the submission call.
    /// </param>
    /// <param name="options">
    /// Polling options. When <see langword="null" />, default polling behavior is used.
    /// </param>
    /// <param name="cancellationToken">
    /// The cancellation token.
    /// </param>
    /// <returns>
    /// The completed multi-layer edit result.
    /// </returns>
    Task<FeatureServiceApplyEditsResult> WaitForApplyEditsCompletionAsync(
        Uri statusUrl,
        ApplyEditsWaitOptions? options = null,
        CancellationToken cancellationToken = default);
}