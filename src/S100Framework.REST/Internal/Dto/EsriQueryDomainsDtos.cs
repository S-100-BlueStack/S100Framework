using System.Text.Json;
using System.Text.Json.Serialization;

namespace S100Framework.REST.Internal.Dto;

internal sealed class EsriQueryDomainsResponseDto
{
    [JsonPropertyName("domains")]
    public List<EsriDomainDto>? Domains { get; init; }
}

internal sealed class EsriDomainDto
{
    [JsonPropertyName("type")]
    public string? Type { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("fieldType")]
    public string? FieldType { get; init; }

    [JsonPropertyName("mergePolicy")]
    public string? MergePolicy { get; init; }

    [JsonPropertyName("splitPolicy")]
    public string? SplitPolicy { get; init; }

    [JsonPropertyName("range")]
    public JsonElement Range { get; init; }

    [JsonPropertyName("codedValues")]
    public List<EsriCodedValueDto>? CodedValues { get; init; }
}

internal sealed class EsriCodedValueDto
{
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("code")]
    public JsonElement Code { get; init; }
}