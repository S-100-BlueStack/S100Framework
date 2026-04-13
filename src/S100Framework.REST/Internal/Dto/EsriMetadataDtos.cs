using System.Text.Json.Serialization;

namespace S100Framework.REST.Internal.Dto;

internal sealed class EsriServiceMetadataDto
{
    [JsonPropertyName("layers")]
    public List<EsriDatasetDto>? Layers { get; init; }

    [JsonPropertyName("tables")]
    public List<EsriDatasetDto>? Tables { get; init; }

    [JsonPropertyName("capabilities")]
    public string? Capabilities { get; init; }

    [JsonPropertyName("maxRecordCount")]
    public int? MaxRecordCount { get; init; }
}

internal sealed class EsriDatasetDto
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }
}

internal sealed class EsriLayerMetadataDto
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("geometryType")]
    public string? GeometryType { get; init; }

    [JsonPropertyName("hasZ")]
    public bool? HasZ { get; init; }

    [JsonPropertyName("hasM")]
    public bool? HasM { get; init; }

    [JsonPropertyName("maxRecordCount")]
    public int? MaxRecordCount { get; init; }

    [JsonPropertyName("objectIdField")]
    public string? ObjectIdField { get; init; }

    [JsonPropertyName("fields")]
    public List<EsriFieldDto>? Fields { get; init; }

    [JsonPropertyName("advancedQueryCapabilities")]
    public EsriAdvancedQueryCapabilitiesDto? AdvancedQueryCapabilities { get; init; }

    [JsonPropertyName("extent")]
    public EsriExtentDto? Extent { get; init; }
}

internal sealed class EsriAdvancedQueryCapabilitiesDto
{
    [JsonPropertyName("supportsPagination")]
    public bool? SupportsPagination { get; init; }
}

internal sealed class EsriFieldDto
{
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("type")]
    public string? Type { get; init; }

    [JsonPropertyName("alias")]
    public string? Alias { get; init; }

    [JsonPropertyName("nullable")]
    public bool? Nullable { get; init; }

    [JsonPropertyName("length")]
    public int? Length { get; init; }
}

internal sealed class EsriExtentDto
{
    [JsonPropertyName("spatialReference")]
    public EsriSpatialReferenceDto? SpatialReference { get; init; }
}

internal sealed class EsriSpatialReferenceDto
{
    [JsonPropertyName("wkid")]
    public int? Wkid { get; init; }

    [JsonPropertyName("latestWkid")]
    public int? LatestWkid { get; init; }
}