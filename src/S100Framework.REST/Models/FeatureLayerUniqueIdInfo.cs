namespace S100Framework.REST.Models;

/// <summary>
/// Describes alternate unique identifiers exposed by a feature layer.
/// </summary>
/// <param name="Type">
/// The unique ID structure type reported by the service, for example <c>simple</c> or <c>composite</c>.
/// </param>
/// <param name="Fields">
/// The fields that participate in the unique ID definition.
/// </param>
/// <param name="OidFieldContainsHashValue">
/// Indicates whether the service generates the object ID field as a hash derived from the unique ID values.
/// </param>
public sealed record FeatureLayerUniqueIdInfo(
    string Type,
    IReadOnlyList<string> Fields,
    bool OidFieldContainsHashValue);