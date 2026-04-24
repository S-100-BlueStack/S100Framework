namespace S100Framework.REST.Models;

/// <summary>
/// Represents the minimum and maximum values of an Esri range domain.
/// </summary>
/// <param name="MinimumValue">
/// The minimum allowed value.
/// </param>
/// <param name="MaximumValue">
/// The maximum allowed value.
/// </param>
public sealed record FeatureServiceDomainRange(
    object? MinimumValue,
    object? MaximumValue);