namespace S100Framework.REST.Models;

/// <summary>
/// Represents an attachment count for a single parent feature.
/// </summary>
/// <param name="SourceObjectId">
/// The parent feature object ID, when available.
/// </param>
/// <param name="SourceGlobalId">
/// The parent feature global ID, when available.
/// </param>
/// <param name="Count">
/// The number of attachments returned by the service for the parent feature.
/// </param>
public sealed record AttachmentCountGroup(
    long? SourceObjectId,
    string? SourceGlobalId,
    long Count);