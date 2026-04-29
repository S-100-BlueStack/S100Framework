using System.Text.Json.Serialization;

namespace S100Framework.REST.Internal.Dto;

internal sealed class EsriCalculateResponseDto
{
    [JsonPropertyName("success")]
    public bool? Success { get; init; }

    [JsonPropertyName("updatedFeatureCount")]
    public long? UpdatedFeatureCount { get; init; }

    [JsonPropertyName("editMoment")]
    public long? EditMoment { get; init; }
}