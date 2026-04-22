namespace S100Framework.REST.Models;

/// <summary>
/// Represents the result of a delete-attachments operation.
/// </summary>
/// <param name="Results">
/// The per-attachment delete results returned by the service.
/// </param>
/// <param name="EditMoment">
/// The edit moment returned by the service, when requested.
/// </param>
public sealed record DeleteAttachmentsResult(
    IReadOnlyList<AttachmentEditResult> Results,
    long? EditMoment);

/// <summary>
/// Represents the result of a single attachment add, update, or delete operation.
/// </summary>
/// <param name="Success">
/// Indicates whether the attachment edit succeeded.
/// </param>
/// <param name="AttachmentId">
/// The attachment ID, when available.
/// </param>
/// <param name="GlobalId">
/// The attachment global ID, when available.
/// </param>
/// <param name="ErrorCode">
/// The service error code, when the operation fails.
/// </param>
/// <param name="ErrorDescription">
/// The service error description, when the operation fails.
/// </param>
public sealed record AttachmentEditResult(
    bool Success,
    long? AttachmentId,
    string? GlobalId,
    int? ErrorCode,
    string? ErrorDescription);