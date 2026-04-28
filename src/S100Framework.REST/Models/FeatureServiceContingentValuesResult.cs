using System.Text.Json;

namespace S100Framework.REST.Models;

/// <summary>
/// Represents the raw contingent values payload returned by a feature service.
/// </summary>
/// <param name="Payload">
/// The raw contingent values JSON payload returned by the service.
/// </param>
public sealed record FeatureServiceContingentValuesResult(
    JsonElement Payload);