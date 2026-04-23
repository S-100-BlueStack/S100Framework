namespace S100Framework.REST.Models;

/// <summary>
/// Represents the count result of a top-features query.
/// </summary>
/// <param name="Count">
/// The number of matching top-feature rows.
/// </param>
/// <param name="Extent">
/// The aggregate extent returned by the service, when available.
/// </param>
public sealed record TopFeaturesCountResult(
    long Count,
    FeatureExtent? Extent);