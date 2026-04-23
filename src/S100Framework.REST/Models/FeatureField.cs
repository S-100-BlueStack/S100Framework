namespace S100Framework.REST.Models;

/// <summary>
/// Describes a field exposed by a layer or table.
/// </summary>
/// <param name="Name">
/// The field name.
/// </param>
/// <param name="EsriType">
/// The ArcGIS field type identifier.
/// </param>
/// <param name="Alias">
/// The field alias, when available.
/// </param>
/// <param name="Nullable">
/// Indicates whether the field accepts null values.
/// </param>
/// <param name="Length">
/// The maximum field length, when applicable.
/// </param>
public sealed record FeatureField(
    string Name,
    string EsriType,
    string? Alias,
    bool Nullable,
    int? Length);