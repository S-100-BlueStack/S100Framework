namespace S100Framework.REST.Models;

/// <summary>
/// Represents a row count returned for one layer or table in a service-level query.
/// </summary>
/// <param name="LayerId">
/// The layer or table ID returned by the service.
/// </param>
/// <param name="Count">
/// The row count returned for the layer or table.
/// </param>
public sealed record FeatureServiceLayerCountResult(
    int LayerId,
    long Count);