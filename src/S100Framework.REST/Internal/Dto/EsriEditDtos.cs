using System.Text.Json.Serialization;

namespace S100Framework.REST.Internal.Dto;

internal sealed class EsriApplyEditsResponseDto
{
    [JsonPropertyName("addResults")]
    public List<EsriEditResultDto>? AddResults { get; init; }

    [JsonPropertyName("updateResults")]
    public List<EsriEditResultDto>? UpdateResults { get; init; }

    [JsonPropertyName("deleteResults")]
    public List<EsriEditResultDto>? DeleteResults { get; init; }
}

internal sealed class EsriEditResultDto
{
    [JsonPropertyName("success")]
    public bool Success { get; init; }

    [JsonPropertyName("objectId")]
    public long? ObjectId { get; init; }

    [JsonPropertyName("globalId")]
    public string? GlobalId { get; init; }

    [JsonPropertyName("error")]
    public EsriEditErrorDto? Error { get; init; }
}

internal sealed class EsriEditErrorDto
{
    [JsonPropertyName("code")]
    public int? Code { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }
}