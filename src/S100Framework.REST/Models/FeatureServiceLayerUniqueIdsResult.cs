namespace S100Framework.REST.Models;

/// <summary>
/// Represents unique IDs returned for one layer or table in a service-level query.
/// </summary>
/// <param name="LayerId">
/// The layer or table ID returned by the service.
/// </param>
/// <param name="UniqueIdFieldNames">
/// The field names that define the unique ID for the layer or table.
/// </param>
/// <param name="UniqueIds">
/// The unique IDs returned for the layer or table.
/// </param>
/// <param name="ExceededTransferLimit">
/// Indicates whether the service exceeded its transfer limit for this layer or table.
/// </param>
public sealed record FeatureServiceLayerUniqueIdsResult(
    int LayerId,
    IReadOnlyList<string> UniqueIdFieldNames,
    IReadOnlyList<FeatureUniqueId> UniqueIds,
    bool? ExceededTransferLimit)
{
    /// <summary>
    /// Gets a value indicating whether the returned identifiers are composite.
    /// </summary>
    public bool IsComposite => UniqueIdFieldNames.Count > 1;
}