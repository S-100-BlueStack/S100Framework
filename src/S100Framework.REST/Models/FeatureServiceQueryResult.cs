namespace S100Framework.REST.Models;

/// <summary>
/// Represents the result of a service-level <c>query</c> operation.
/// </summary>
/// <param name="Layers">
/// The layer-grouped query results returned by the service.
/// </param>
public sealed record FeatureServiceQueryResult(
    IReadOnlyList<FeatureServiceLayerQueryResult> Layers);