namespace S100Framework.REST.Models;

/// <summary>
/// Represents the related records associated with a single source feature.
/// </summary>
/// <param name="SourceObjectId">
/// The object ID of the source feature.
/// </param>
/// <param name="Records">
/// The related records returned for the source feature.
/// </param>
public sealed record RelatedRecordGroup(
    long SourceObjectId,
    IReadOnlyList<FeatureRecord> Records);