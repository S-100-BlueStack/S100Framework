namespace S100Framework.REST.Models;

public sealed record DeleteAttachmentsResult(
    IReadOnlyList<AttachmentEditResult> Results,
    long? EditMoment);

public sealed record AttachmentEditResult(
    bool Success,
    long? AttachmentId,
    string? GlobalId,
    int? ErrorCode,
    string? ErrorDescription);