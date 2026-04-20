using S100Framework.REST.Models;

namespace S100Framework.REST.Abstractions;

public interface IFeatureLayerClient
{
    Task<FeatureLayerSchema> GetSchemaAsync(CancellationToken cancellationToken = default);

    IAsyncEnumerable<FeatureRecord> QueryAsync(
        FeatureQuery query,
        CancellationToken cancellationToken = default);

    Task<long> QueryCountAsync(
        FeatureQuery query,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<long>> QueryObjectIdsAsync(
        FeatureQuery query,
        CancellationToken cancellationToken = default);

    Task<FeatureExtent?> QueryExtentAsync(
        FeatureQuery query,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StatisticRow>> QueryStatisticsAsync(
        FeatureStatisticsQuery query,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RelatedRecordGroup>> QueryRelatedRecordsAsync(
        RelatedRecordsQuery query,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AttachmentGroup>> QueryAttachmentsAsync(
        AttachmentQuery query,
        CancellationToken cancellationToken = default);

    Task<AttachmentContent> DownloadAttachmentAsync(
        long objectId,
        long attachmentId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FeatureRecord>> QueryTopFeaturesAsync(
        TopFeaturesQuery query,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<long>> QueryTopFeatureObjectIdsAsync(
        TopFeaturesQuery query,
        CancellationToken cancellationToken = default);

    Task<TopFeaturesCountResult> QueryTopFeatureCountAsync(
        TopFeaturesQuery query,
        CancellationToken cancellationToken = default);

    Task<ApplyEditsResult> ApplyEditsAsync(
        FeatureEdits edits,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Submits a layer-level <c>applyEdits</c> request as an asynchronous job when the server supports it.
    /// </summary>
    /// <param name="edits">The edits to submit for the current layer.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>
    /// A submission response that contains either an immediate result or a status URL that can be polled.
    /// </returns>
    Task<ApplyEditsSubmissionResult> SubmitApplyEditsAsync(
        FeatureEdits edits,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current status of a previously submitted layer-level <c>applyEdits</c> job.
    /// </summary>
    /// <param name="statusUrl">The status URL returned from the submission call.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The current job status.</returns>
    Task<ApplyEditsJobStatus> GetApplyEditsStatusAsync(
        Uri statusUrl,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the result payload for a completed layer-level <c>applyEdits</c> job.
    /// </summary>
    /// <param name="resultUrl">The result URL returned by the status resource.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The completed edit result.</returns>
    Task<ApplyEditsResult> GetApplyEditsResultAsync(
        Uri resultUrl,
        CancellationToken cancellationToken = default);

    Task<DeleteAttachmentsResult> DeleteAttachmentsAsync(
        DeleteAttachmentsRequest request,
        CancellationToken cancellationToken = default);

    Task<AddAttachmentResult> AddAttachmentAsync(
        AddAttachmentRequest request,
        CancellationToken cancellationToken = default);

    Task<UpdateAttachmentResult> UpdateAttachmentAsync(
        UpdateAttachmentRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Submits a layer-level <c>applyEdits</c> request and waits until the job completes.
    /// </summary>
    /// <param name="edits">The edits to submit for the current layer.</param>
    /// <param name="options">
    /// Polling options. When <see langword="null"/>, default polling behavior is used.
    /// </param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The completed edit result.</returns>
    Task<ApplyEditsResult> WaitForApplyEditsCompletionAsync(
        FeatureEdits edits,
        ApplyEditsWaitOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Polls an existing layer-level <c>applyEdits</c> job until it completes.
    /// </summary>
    /// <param name="statusUrl">The status URL returned from the submission call.</param>
    /// <param name="options">
    /// Polling options. When <see langword="null"/>, default polling behavior is used.
    /// </param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The completed edit result.</returns>
    Task<ApplyEditsResult> WaitForApplyEditsCompletionAsync(
        Uri statusUrl,
        ApplyEditsWaitOptions? options = null,
        CancellationToken cancellationToken = default);
}