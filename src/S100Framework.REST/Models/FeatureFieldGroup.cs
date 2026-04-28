namespace S100Framework.REST.Models;

/// <summary>
/// Represents a field group defined on a feature layer or table.
/// </summary>
/// <param name="Name">
/// The field group name.
/// </param>
/// <param name="Restrictive">
/// Indicates whether the field group is restrictive, when returned by the service.
/// </param>
/// <param name="Fields">
/// The fields included in the field group.
/// </param>
public sealed record FeatureFieldGroup(
    string Name,
    bool? Restrictive,
    IReadOnlyList<string> Fields);