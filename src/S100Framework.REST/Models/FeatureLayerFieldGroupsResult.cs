namespace S100Framework.REST.Models;

/// <summary>
/// Represents the field groups returned for a single feature layer or table.
/// </summary>
/// <param name="LayerId">
/// The layer or table ID associated with the field groups.
/// </param>
/// <param name="FieldGroups">
/// The field groups returned by the service.
/// </param>
public sealed record FeatureLayerFieldGroupsResult(
    int LayerId,
    IReadOnlyList<FeatureFieldGroup> FieldGroups);