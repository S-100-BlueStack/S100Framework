using S100Framework.REST.Models;

namespace S100Framework.REST.Abstractions;

/// <summary>
/// Defines attachment operations for a feature layer endpoint.
/// </summary>
public partial interface IFeatureLayerClient
{
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
    /// Queries attachment counts for matching parent features in the current layer.
    /// </summary>
    /// <param name="query">
    /// The attachment query to execute.
    /// </param>
    /// <param name="cancellationToken">
    /// The cancellation token.
    /// </param>
    /// <returns>
    /// The attachment counts grouped by parent feature.
    /// </returns>
    Task<IReadOnlyList<AttachmentCountGroup>> QueryAttachmentCountsAsync(
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
}