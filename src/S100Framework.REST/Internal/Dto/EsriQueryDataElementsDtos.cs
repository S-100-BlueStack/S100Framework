using System.Text.Json;
using System.Text.Json.Serialization;

namespace S100Framework.REST.Internal.Dto;

internal sealed class EsriQueryDataElementsResponseDto
{
    [JsonPropertyName("layerDataElements")]
    public List<EsriLayerDataElementDto?>? LayerDataElements { get; init; }
}

internal sealed class EsriLayerDataElementDto
{
    [JsonPropertyName("layerId")]
    public int? LayerId { get; init; }

    [JsonPropertyName("dataElement")]
    public JsonElement? DataElement { get; init; }
}