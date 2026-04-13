using System.Text.Json;
using System.Text.Json.Serialization;

namespace S100Framework.REST.Internal.Dto;

internal sealed class EsriQueryResponseDto
{
    [JsonPropertyName("objectIdFieldName")]
    public string? ObjectIdFieldName { get; init; }

    [JsonPropertyName("features")]
    public List<EsriFeatureDto>? Features { get; init; }

    [JsonPropertyName("exceededTransferLimit")]
    public bool? ExceededTransferLimit { get; init; }
}

internal sealed class EsriFeatureDto
{
    [JsonPropertyName("attributes")]
    public JsonElement Attributes { get; init; }

    [JsonPropertyName("geometry")]
    public JsonElement Geometry { get; init; }
}

internal sealed class EsriIdsResponseDto
{
    [JsonPropertyName("objectIdFieldName")]
    public string? ObjectIdFieldName { get; init; }

    [JsonPropertyName("objectIds")]
    public List<long>? ObjectIds { get; init; }
}