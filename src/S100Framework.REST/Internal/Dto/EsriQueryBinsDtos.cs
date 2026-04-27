using System.Text.Json;
using System.Text.Json.Serialization;

namespace S100Framework.REST.Internal.Dto;

internal sealed class EsriQueryBinsResponseDto
{
    [JsonPropertyName("features")]
    public List<EsriQueryBinFeatureDto>? Features { get; init; }

    [JsonPropertyName("exceededTransferLimit")]
    public bool? ExceededTransferLimit { get; init; }

    [JsonPropertyName("stackFieldNames")]
    public List<string>? StackFieldNames { get; init; }
}

internal sealed class EsriQueryBinFeatureDto
{
    [JsonPropertyName("attributes")]
    public JsonElement Attributes { get; init; }

    [JsonPropertyName("stackedAttributes")]
    public List<JsonElement>? StackedAttributes { get; init; }
}