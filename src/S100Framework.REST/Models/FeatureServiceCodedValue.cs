namespace S100Framework.REST.Models;

/// <summary>
/// Represents a single coded value entry in an Esri domain.
/// </summary>
/// <param name="Name">
/// The display name of the coded value.
/// </param>
/// <param name="Code">
/// The underlying code value.
/// </param>
public sealed record FeatureServiceCodedValue(
    string Name,
    object? Code);