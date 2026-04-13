using System.Text.Json.Serialization;

namespace S100Framework.REST.Internal.Dto;

internal sealed class EsriErrorEnvelopeDto
{
    [JsonPropertyName("error")]
    public EsriErrorDto? Error { get; init; }
}

internal sealed class EsriErrorDto
{
    [JsonPropertyName("code")]
    public int? Code { get; init; }

    [JsonPropertyName("message")]
    public string? Message { get; init; }

    [JsonPropertyName("details")]
    public List<string>? Details { get; init; }
}