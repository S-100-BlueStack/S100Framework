using System.Text.Json;

namespace S100Framework.REST.Models;

/// <summary>
/// Represents contingent values returned for a single feature layer or table.
/// </summary>
/// <param name="LayerId">
/// The layer or table ID associated with the query.
/// </param>
/// <param name="Payload">
/// The raw contingent values JSON payload returned by the service.
/// </param>
public sealed record FeatureLayerContingentValuesResult(
    int LayerId,
    JsonElement Payload);