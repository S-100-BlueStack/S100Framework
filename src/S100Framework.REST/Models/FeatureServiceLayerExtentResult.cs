namespace S100Framework.REST.Models;

/// <summary>
/// Represents an extent returned for one layer in a service-client extent query.
/// </summary>
/// <param name="LayerId">
/// The layer ID returned by the service.
/// </param>
/// <param name="Extent">
/// The extent returned for the layer, or <see langword="null"/> when the service did not return a complete extent.
/// </param>
public sealed record FeatureServiceLayerExtentResult(
    int LayerId,
    FeatureExtent? Extent);