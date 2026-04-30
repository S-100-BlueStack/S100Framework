namespace S100Framework.REST.Models;

/// <summary>
/// Represents object IDs returned for one layer or table in a service-level query.
/// </summary>
/// <param name="LayerId">
/// The layer or table ID returned by the service.
/// </param>
/// <param name="ObjectIdFieldName">
/// The object ID field name returned by the service.
/// </param>
/// <param name="ObjectIds">
/// The object IDs returned for the layer or table.
/// </param>
public sealed record FeatureServiceLayerObjectIdsResult(
    int LayerId,
    string? ObjectIdFieldName,
    IReadOnlyList<long> ObjectIds);