using S100Framework.REST.Models;

namespace S100Framework.REST.Abstractions;

/// <summary>
/// Defines layer-level edit operations for a feature layer endpoint.
/// </summary>
public partial interface IFeatureLayerClient
{
    /// <summary>
    /// Sends a layer-level <c>applyEdits</c> request and waits for the server to return
    /// the final edit result.
    /// </summary>
    /// <param name="edits">
    /// The edits to apply to the current layer.
    /// </param>
    /// <param name="cancellationToken">
    /// The cancellation token.
    /// </param>
    /// <returns>
    /// The final edit result returned by the server.
    /// </returns>
    Task<ApplyEditsResult> ApplyEditsAsync(
        FeatureEdits edits,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds features to the current layer by wrapping a layer-level <c>applyEdits</c> request.
    /// </summary>
    /// <param name="features">
    /// The features to add.
    /// </param>
    /// <param name="options">
    /// Optional edit behavior. When <see langword="null" />, default edit behavior is used.
    /// </param>
    /// <param name="cancellationToken">
    /// The cancellation token.
    /// </param>
    /// <returns>
    /// The per-feature add results returned by the service.
    /// </returns>
    Task<IReadOnlyList<EditResult>> AddFeaturesAsync(
        IReadOnlyList<EditableFeature> features,
        FeatureEditOptions? options = null,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This feature layer client does not support add feature convenience operations.");

    /// <summary>
    /// Updates features in the current layer by wrapping a layer-level <c>applyEdits</c> request.
    /// </summary>
    /// <param name="features">
    /// The features to update.
    /// </param>
    /// <param name="options">
    /// Optional edit behavior. When <see langword="null" />, default edit behavior is used.
    /// </param>
    /// <param name="cancellationToken">
    /// The cancellation token.
    /// </param>
    /// <returns>
    /// The per-feature update results returned by the service.
    /// </returns>
    Task<IReadOnlyList<EditResult>> UpdateFeaturesAsync(
        IReadOnlyList<EditableFeature> features,
        FeatureEditOptions? options = null,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This feature layer client does not support update feature convenience operations.");

    /// <summary>
    /// Deletes features from the current layer by object ID by wrapping a layer-level <c>applyEdits</c> request.
    /// </summary>
    /// <param name="objectIds">
    /// The object IDs to delete.
    /// </param>
    /// <param name="options">
    /// Optional edit behavior. When <see langword="null" />, default edit behavior is used.
    /// </param>
    /// <param name="cancellationToken">
    /// The cancellation token.
    /// </param>
    /// <returns>
    /// The per-feature delete results returned by the service.
    /// </returns>
    Task<IReadOnlyList<EditResult>> DeleteFeaturesAsync(
        IReadOnlyList<long> objectIds,
        FeatureEditOptions? options = null,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This feature layer client does not support delete feature convenience operations.");

    /// <summary>
    /// Submits a layer-level <c>applyEdits</c> request as an asynchronous job when the
    /// server supports it.
    /// </summary>
    /// <param name="edits">
    /// The edits to submit for the current layer.
    /// </param>
    /// <param name="cancellationToken">
    /// The cancellation token.
    /// </param>
    /// <returns>
    /// A submission response that contains either an immediate result or a status URL
    /// that can be polled.
    /// </returns>
    ///

    Task<ApplyEditsSubmissionResult> SubmitApplyEditsAsync(
        FeatureEdits edits,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current status of a previously submitted layer-level
    /// <c>applyEdits</c> job.
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
    /// Gets the result payload for a completed layer-level <c>applyEdits</c> job.
    /// </summary>
    /// <param name="resultUrl">
    /// The result URL returned by the status resource.
    /// </param>
    /// <param name="cancellationToken">
    /// The cancellation token.
    /// </param>
    /// <returns>
    /// The completed edit result.
    /// </returns>
    Task<ApplyEditsResult> GetApplyEditsResultAsync(
        Uri resultUrl,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Submits a layer-level <c>applyEdits</c> request and waits until the job completes.
    /// </summary>
    /// <param name="edits">
    /// The edits to submit for the current layer.
    /// </param>
    /// <param name="options">
    /// Polling options. When <see langword="null" />, default polling behavior is used.
    /// </param>
    /// <param name="cancellationToken">
    /// The cancellation token.
    /// </param>
    /// <returns>
    /// The completed edit result.
    /// </returns>
    Task<ApplyEditsResult> WaitForApplyEditsCompletionAsync(
        FeatureEdits edits,
        ApplyEditsWaitOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Polls an existing layer-level <c>applyEdits</c> job until it completes.
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
    /// The completed edit result.
    /// </returns>
    Task<ApplyEditsResult> WaitForApplyEditsCompletionAsync(
        Uri statusUrl,
        ApplyEditsWaitOptions? options = null,
        CancellationToken cancellationToken = default);
}