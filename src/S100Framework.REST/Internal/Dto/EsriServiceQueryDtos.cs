using System.Text.Json;
using System.Text.Json.Serialization;

namespace S100Framework.REST.Internal.Dto;

internal sealed class EsriServiceQueryResponseDto
{
    [JsonPropertyName("layers")]
    public List<EsriServiceQueryLayerDto?>? Layers { get; init; }
}

internal sealed class EsriServiceQueryLayerDto
{
    [JsonPropertyName("id")]
    public int? Id { get; init; }

    [JsonPropertyName("objectIdFieldName")]
    public string? ObjectIdFieldName { get; init; }

    [JsonPropertyName("globalIdFieldName")]
    public string? GlobalIdFieldName { get; init; }

    [JsonPropertyName("geometryType")]
    public string? GeometryType { get; init; }

    [JsonPropertyName("spatialReference")]
    public EsriSpatialReferenceDto? SpatialReference { get; init; }

    [JsonPropertyName("hasZ")]
    public bool? HasZ { get; init; }

    [JsonPropertyName("hasM")]
    public bool? HasM { get; init; }

    [JsonPropertyName("fields")]
    public List<EsriFieldDto>? Fields { get; init; }

    [JsonPropertyName("features")]
    public List<EsriFeatureDto?>? Features { get; init; }

    [JsonPropertyName("count")]
    public long? Count { get; init; }

    [JsonPropertyName("objectIds")]
    public List<long?>? ObjectIds { get; init; }

    [JsonPropertyName("uniqueIdFieldNames")]
    public JsonElement UniqueIdFieldNames { get; init; }

    [JsonPropertyName("uniqueIds")]
    public JsonElement UniqueIds { get; init; }

    [JsonPropertyName("exceededTransferLimit")]
    public bool? ExceededTransferLimit { get; init; }
}