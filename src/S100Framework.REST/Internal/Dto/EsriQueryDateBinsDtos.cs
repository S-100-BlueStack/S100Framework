using System.Text.Json;
using System.Text.Json.Serialization;

namespace S100Framework.REST.Internal.Dto;

internal sealed class EsriQueryDateBinsResponseDto
{
    [JsonPropertyName("features")]
    public List<EsriQueryDateBinFeatureDto?>? Features { get; init; }

    [JsonPropertyName("exceededTransferLimit")]
    public bool? ExceededTransferLimit { get; init; }

    [JsonPropertyName("geometryType")]
    public string? GeometryType { get; init; }
}

internal sealed class EsriQueryDateBinFeatureDto
{
    [JsonPropertyName("attributes")]
    public JsonElement Attributes { get; init; }

    [JsonPropertyName("centroid")]
    public JsonElement? Centroid { get; init; }
}