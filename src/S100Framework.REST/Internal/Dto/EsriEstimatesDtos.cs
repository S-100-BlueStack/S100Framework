using System.Text.Json.Serialization;

namespace S100Framework.REST.Internal.Dto;

internal sealed class EsriServiceEstimatesResponseDto
{
    [JsonPropertyName("layerEstimates")]
    public List<EsriLayerEstimateDto>? LayerEstimates { get; init; }
}

internal sealed class EsriLayerEstimateDto
{
    [JsonPropertyName("layerId")]
    public int? LayerId { get; init; }

    [JsonPropertyName("count")]
    public long? Count { get; init; }

    [JsonPropertyName("extent")]
    public EsriEstimateExtentDto? Extent { get; init; }
}

internal sealed class EsriEstimateExtentDto
{
    [JsonPropertyName("xmin")]
    public double? XMin { get; init; }

    [JsonPropertyName("ymin")]
    public double? YMin { get; init; }

    [JsonPropertyName("xmax")]
    public double? XMax { get; init; }

    [JsonPropertyName("ymax")]
    public double? YMax { get; init; }

    [JsonPropertyName("spatialReference")]
    public EsriSpatialReferenceDto? SpatialReference { get; init; }
}