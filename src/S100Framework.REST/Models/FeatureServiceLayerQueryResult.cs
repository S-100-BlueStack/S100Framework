namespace S100Framework.REST.Models;

/// <summary>
/// Represents query results returned for one layer or table in a service-level query.
/// </summary>
/// <param name="LayerId">
/// The layer or table ID returned by the service.
/// </param>
/// <param name="Records">
/// The feature records returned for the layer or table.
/// </param>
/// <param name="ExceededTransferLimit">
/// Indicates whether the service exceeded its transfer limit for this layer or table.
/// </param>
public sealed record FeatureServiceLayerQueryResult(
    int LayerId,
    IReadOnlyList<FeatureRecord> Records,
    bool? ExceededTransferLimit);