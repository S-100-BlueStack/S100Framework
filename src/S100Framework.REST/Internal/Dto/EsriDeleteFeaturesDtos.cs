using System.Text.Json.Serialization;

namespace S100Framework.REST.Internal.Dto;

internal sealed class EsriDeleteFeaturesResponseDto
{
    [JsonPropertyName("success")]
    public bool? Success { get; init; }

    [JsonPropertyName("deleteResults")]
    public List<EsriEditResultDto>? DeleteResults { get; init; }

    [JsonPropertyName("editMoment")]
    public long? EditMoment { get; init; }
}