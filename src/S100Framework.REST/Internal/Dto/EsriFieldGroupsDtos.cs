using System.Text.Json.Serialization;

namespace S100Framework.REST.Internal.Dto;

internal sealed class EsriFieldGroupsResponseDto
{
    [JsonPropertyName("fieldGroups")]
    public List<EsriFieldGroupDto>? FieldGroups { get; init; }
}

internal sealed class EsriFieldGroupDto
{
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("restrictive")]
    public bool? Restrictive { get; init; }

    [JsonPropertyName("fields")]
    public List<string>? Fields { get; init; }
}