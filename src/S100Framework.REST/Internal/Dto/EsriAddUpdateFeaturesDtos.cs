using System.Text.Json.Serialization;

namespace S100Framework.REST.Internal.Dto;

internal sealed class EsriAddFeaturesResponseDto
{
    [JsonPropertyName("addResults")]
    public List<EsriEditResultDto>? AddResults { get; init; }

    [JsonPropertyName("editMoment")]
    public long? EditMoment { get; init; }
}

internal sealed class EsriUpdateFeaturesResponseDto
{
    [JsonPropertyName("updateResults")]
    public List<EsriEditResultDto>? UpdateResults { get; init; }

    [JsonPropertyName("editMoment")]
    public long? EditMoment { get; init; }
}