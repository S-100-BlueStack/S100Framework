namespace S100Framework.REST.Models;

/// <summary>
/// Represents attachment metadata returned by a layer attachment query.
/// </summary>
/// <param name="AttachmentId">
/// The attachment ID.
/// </param>
/// <param name="ParentObjectId">
/// The parent feature object ID, when available.
/// </param>
/// <param name="ParentGlobalId">
/// The parent feature global ID, when available.
/// </param>
/// <param name="Name">
/// The attachment file name, when available.
/// </param>
/// <param name="ContentType">
/// The attachment MIME type, when available.
/// </param>
/// <param name="Size">
/// The attachment size in bytes, when available.
/// </param>
/// <param name="GlobalId">
/// The attachment global ID, when available.
/// </param>
/// <param name="Url">
/// The attachment URL, when returned by the service.
/// </param>
/// <param name="Attributes">
/// Any additional attachment attributes returned by the service.
/// </param>
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