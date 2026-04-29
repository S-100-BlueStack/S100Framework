namespace S100Framework.REST.Models;

/// <summary>
/// Represents the result returned by a feature service <c>relationships</c> resource.
/// </summary>
/// <param name="Relationships">
/// The relationship classes returned by the feature service.
/// </param>
public sealed record FeatureServiceRelationshipsResult(
    IReadOnlyList<FeatureServiceRelationship> Relationships);