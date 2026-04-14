namespace S100Framework.REST.Models;

public sealed record AttachmentGroup(
    long? SourceObjectId,
    string? SourceGlobalId,
    IReadOnlyList<AttachmentInfo> Attachments);