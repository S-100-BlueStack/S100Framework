namespace S100Framework.REST.Models;

/// <summary>
/// Represents the attachments associated with a single parent feature.
/// </summary>
/// <param name="SourceObjectId">
/// The parent feature object ID, when available.
/// </param>
/// <param name="SourceGlobalId">
/// The parent feature global ID, when available.
/// </param>
/// <param name="Attachments">
/// The attachments associated with the parent feature.
/// </param>
public sealed record AttachmentGroup(
    long? SourceObjectId,
    string? SourceGlobalId,
    IReadOnlyList<AttachmentInfo> Attachments);