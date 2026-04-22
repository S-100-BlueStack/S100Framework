using S100Framework.REST.Models;

namespace S100Framework.REST.Abstractions;

/// <summary>
/// Defines the public contract for working with a single feature layer or table.
/// </summary>
/// <remarks>
/// Implementations provide query, attachment, and edit operations scoped to one
/// resolved layer or table in a feature service.
/// </remarks>
public interface IFeatureLayerClient
{
    /// <summary>
    /// Gets the schema metadata for the current layer or table.
    /// </summary>
    /// <param name="cancellationToken">
    /// The cancellation token.
    /// </param>
    /// <returns>
    /// The layer schema metadata.
    /// </returns>
    Task<FeatureLayerSchema> GetSchemaAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a feature query and streams the matching records.
    /// </summary>
    /// <param name="query">
    /// The feature query to execute.
    /// </param>
    /// <param name="cancellationToken">
    /// The cancellation token.
    /// </param>
    /// <returns>
    /// An async stream of matching feature records.
    /// </returns>
    IAsyncEnumerable<FeatureRecord> QueryAsync(
        FeatureQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a feature query and returns only the total record count.
    /// </summary>
    /// <param name="query">
    /// The feature query to execute.
    /// </param>
    /// <param name="cancellationToken">
    /// The cancellation token.
    /// </param>
    /// <returns>
    /// The total number of matching records.
    /// </returns>
    Task<long> QueryCountAsync(
        FeatureQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a feature query and returns only the matching object IDs.
    /// </summary>
    /// <param name="query">
    /// The feature query to execute.
    /// </param>
    /// <param name="cancellationToken">
    /// The cancellation token.
    /// </param>
    /// <returns>
    /// The matching object IDs.
    /// </returns>
    Task<IReadOnlyList<long>> QueryObjectIdsAsync(
        FeatureQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a feature query and returns the aggregate extent of the matching records.
    /// </summary>
    /// <param name="query">
    /// The feature query to execute.
    /// </param>
    /// <param name="cancellationToken">
    /// The cancellation token.
    /// </param>
    /// <returns>
    /// The matching extent, or <see langword="null" /> when no extent is returned.
    /// </returns>
    Task<FeatureExtent?> QueryExtentAsync(
        FeatureQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a statistics query for the current layer or table.
    /// </summary>
    /// <param name="query">
    /// The statistics query to execute.
    /// </param>
    /// <param name="cancellationToken">
    /// The cancellation token.
    /// </param>
    /// <returns>
    /// The resulting statistic rows.
    /// </returns>
    Task<IReadOnlyList<StatisticRow>> QueryStatisticsAsync(
        FeatureStatisticsQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a related-records query for the current layer.
    /// </summary>
    /// <param name="query">
    /// The related-records query to execute.
    /// </param>
    /// <param name="cancellationToken">
    /// The cancellation token.
    /// </param>
    /// <returns>
    /// The related record groups returned by the service.
    /// </returns>
    Task<IReadOnlyList<RelatedRecordGroup>> QueryRelatedRecordsAsync(
        RelatedRecordsQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Queries attachment metadata for the current layer.
    /// </summary>
    /// <param name="query">
    /// The attachment query to execute.
    /// </param>
    /// <param name="cancellationToken">
    /// The cancellation token.
    /// </param>
    /// <returns>
    /// The attachment groups returned by the service.
    /// </returns>
    Task<IReadOnlyList<AttachmentGroup>> QueryAttachmentsAsync(
        AttachmentQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads a specific attachment for a feature in the current layer.
    /// </summary>
    /// <param name="objectId">
    /// The object ID of the parent feature.
    /// </param>
    /// <param name="attachmentId">
    /// The attachment ID to download.
    /// </param>
    /// <param name="cancellationToken">
    /// The cancellation token.
    /// </param>
    /// <returns>
    /// The downloaded attachment content.
    /// </returns>
    Task<AttachmentContent> DownloadAttachmentAsync(
        long objectId,
        long attachmentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a top-features query for the current layer.
    /// </summary>
    /// <param name="query">
    /// The top-features query to execute.
    /// </param>
    /// <param name="cancellationToken">
    /// The cancellation token.
    /// </param>
    /// <returns>
    /// The matching top features.
    /// </returns>
    Task<IReadOnlyList<FeatureRecord>> QueryTopFeaturesAsync(
        TopFeaturesQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a top-features query and returns only the matching object IDs.
    /// </summary>
    /// <param name="query">
    /// The top-features query to execute.
    /// </param>
    /// <param name="cancellationToken">
    /// The cancellation token.
    /// </param>
    /// <returns>
    /// The matching top-feature object IDs.
    /// </returns>
    Task<IReadOnlyList<long>> QueryTopFeatureObjectIdsAsync(
        TopFeaturesQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a top-features query and returns only the count result.
    /// </summary>
    /// <param name="query">
    /// The top-features query to execute.
    /// </param>
    /// <param name="cancellationToken">
    /// The cancellation token.
    /// </param>
    /// <returns>
    /// The top-features count result.
    /// </returns>
    Task<TopFeaturesCountResult> QueryTopFeatureCountAsync(
        TopFeaturesQuery query,
        CancellationToken cancellationToken = default);

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
    /// Deletes one or more attachments from the current layer.
    /// </summary>
    /// <param name="request">
    /// The delete-attachments request.
    /// </param>
    /// <param name="cancellationToken">
    /// The cancellation token.
    /// </param>
    /// <returns>
    /// The delete-attachments result.
    /// </returns>
    Task<DeleteAttachmentsResult> DeleteAttachmentsAsync(
        DeleteAttachmentsRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds an attachment to the current layer.
    /// </summary>
    /// <param name="request">
    /// The add-attachment request.
    /// </param>
    /// <param name="cancellationToken">
    /// The cancellation token.
    /// </param>
    /// <returns>
    /// The add-attachment result.
    /// </returns>
    Task<AddAttachmentResult> AddAttachmentAsync(
        AddAttachmentRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing attachment in the current layer.
    /// </summary>
    /// <param name="request">
    /// The update-attachment request.
    /// </param>
    /// <param name="cancellationToken">
    /// The cancellation token.
    /// </param>
    /// <returns>
    /// The update-attachment result.
    /// </returns>
    Task<UpdateAttachmentResult> UpdateAttachmentAsync(
        UpdateAttachmentRequest request,
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