namespace S100Framework.REST.Models;

public sealed record AttachmentInfo(
    long AttachmentId,
    long? ParentObjectId,
    string? ParentGlobalId,
    string? Name,
    string? ContentType,
    long? Size,
    string? GlobalId,
    string? Url,
    IReadOnlyDictionary<string, object?> Attributes) : IAttributeRecord;