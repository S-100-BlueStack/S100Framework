using System.Text.Json;

namespace S100Framework.REST.Models;

/// <summary>
/// Represents a raw data element returned by a feature service <c>queryDataElements</c> operation.
/// </summary>
/// <param name="LayerId">
/// The layer or table ID associated with the data element.
/// </param>
/// <param name="DataElement">
/// The raw data element JSON returned by the service.
/// </param>
public sealed record FeatureLayerDataElement(
    int LayerId,
    JsonElement DataElement);