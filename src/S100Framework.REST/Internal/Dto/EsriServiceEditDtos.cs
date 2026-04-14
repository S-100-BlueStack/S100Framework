using System.Text.Json.Serialization;

namespace S100Framework.REST.Internal.Dto;

internal sealed class EsriServiceLayerEditResultsDto
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("addResults")]
    public List<EsriEditResultDto>? AddResults { get; init; }

    [JsonPropertyName("updateResults")]
    public List<EsriEditResultDto>? UpdateResults { get; init; }

    [JsonPropertyName("deleteResults")]
    public List<EsriEditResultDto>? DeleteResults { get; init; }
}