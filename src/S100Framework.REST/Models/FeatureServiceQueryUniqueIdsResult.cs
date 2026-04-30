namespace S100Framework.REST.Models;

/// <summary>
/// Represents the result of a service-level <c>query</c> operation that returns unique IDs only.
/// </summary>
/// <param name="Layers">
/// The layer-grouped unique IDs returned by the service.
/// </param>
public sealed record FeatureServiceQueryUniqueIdsResult(
    IReadOnlyList<FeatureServiceLayerUniqueIdsResult> Layers);