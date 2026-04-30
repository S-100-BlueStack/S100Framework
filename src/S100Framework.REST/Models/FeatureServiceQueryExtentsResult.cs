namespace S100Framework.REST.Models;

/// <summary>
/// Represents the result of querying extents for multiple feature service layers.
/// </summary>
/// <param name="Layers">
/// The layer-grouped extents returned by the service.
/// </param>
public sealed record FeatureServiceQueryExtentsResult(
    IReadOnlyList<FeatureServiceLayerExtentResult> Layers);