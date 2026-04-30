namespace S100Framework.REST.Models;

/// <summary>
/// Represents the result of a service-level <c>query</c> operation that returns object IDs only.
/// </summary>
/// <param name="Layers">
/// The layer-grouped object IDs returned by the service.
/// </param>
public sealed record FeatureServiceQueryObjectIdsResult(
    IReadOnlyList<FeatureServiceLayerObjectIdsResult> Layers);