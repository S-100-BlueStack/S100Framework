namespace S100Framework.REST.Models;

/// <summary>
/// Represents a related-record count for a single source feature.
/// </summary>
/// <param name="SourceObjectId">
/// The object ID of the source feature.
/// </param>
/// <param name="Count">
/// The number of related records returned by the service.
/// </param>
public sealed record RelatedRecordCountGroup(
    long SourceObjectId,
    long Count);