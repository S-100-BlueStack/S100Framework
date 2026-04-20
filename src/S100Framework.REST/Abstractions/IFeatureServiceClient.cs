using S100Framework.REST.Models;

namespace S100Framework.REST.Abstractions;

public interface IFeatureServiceClient
{
    Task<FeatureServiceMetadata> GetMetadataAsync(CancellationToken cancellationToken = default);

    IFeatureLayerClient GetLayerClient(int layerId);

    Task<FeatureServiceApplyEditsResult> ApplyEditsAsync(
        FeatureServiceEdits edits,
        CancellationToken cancellationToken = default);

    Task<IFeatureLayerClient> GetLayerClientAsync(
    string layerName,
    CancellationToken cancellationToken = default);

    Task<ExtractChangesResult> ExtractChangesAsync(
        ExtractChangesRequest request,
        CancellationToken cancellationToken = default);

    Task<ExtractChangesSubmissionResult> SubmitExtractChangesAsync(
    ExtractChangesRequest request,
    CancellationToken cancellationToken = default);

    Task<ExtractChangesJobStatus> GetExtractChangesStatusAsync(
        Uri statusUrl,
        CancellationToken cancellationToken = default);

    Task<ExtractChangesFileResult> DownloadExtractChangesFileAsync(
        Uri resultUrl,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Submits a service-level <c>applyEdits</c> request as an asynchronous job when the server supports it.
    /// </summary>
    /// <param name="edits">The multi-layer edits to submit.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>
    /// A submission response that contains either an immediate result or a status URL that can be polled.
    /// </returns>
    Task<FeatureServiceApplyEditsSubmissionResult> SubmitApplyEditsAsync(
        FeatureServiceEdits edits,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current status of a previously submitted service-level <c>applyEdits</c> job.
    /// </summary>
    /// <param name="statusUrl">The status URL returned from the submission call.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The current job status.</returns>
    Task<ApplyEditsJobStatus> GetApplyEditsStatusAsync(
        Uri statusUrl,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the result payload for a completed service-level <c>applyEdits</c> job.
    /// </summary>
    /// <param name="resultUrl">The result URL returned by the status resource.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The completed multi-layer edit result.</returns>
    Task<FeatureServiceApplyEditsResult> GetApplyEditsResultAsync(
        Uri resultUrl,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Submits a service-level <c>applyEdits</c> request and waits until the job completes.
    /// </summary>
    /// <param name="edits">The multi-layer edits to submit.</param>
    /// <param name="options">
    /// Polling options. When <see langword="null"/>, default polling behavior is used.
    /// </param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The completed multi-layer edit result.</returns>
    Task<FeatureServiceApplyEditsResult> WaitForApplyEditsCompletionAsync(
        FeatureServiceEdits edits,
        ApplyEditsWaitOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Polls an existing service-level <c>applyEdits</c> job until it completes.
    /// </summary>
    /// <param name="statusUrl">The status URL returned from the submission call.</param>
    /// <param name="options">
    /// Polling options. When <see langword="null"/>, default polling behavior is used.
    /// </param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The completed multi-layer edit result.</returns>
    Task<FeatureServiceApplyEditsResult> WaitForApplyEditsCompletionAsync(
        Uri statusUrl,
        ApplyEditsWaitOptions? options = null,
        CancellationToken cancellationToken = default);
}