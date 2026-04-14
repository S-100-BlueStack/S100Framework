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

    [JsonPropertyName("hasAttachments")]
    public bool? HasAttachments { get; init; }

    [JsonPropertyName("supportsQueryAttachments")]
    public bool? SupportsQueryAttachments { get; init; }

    [JsonPropertyName("supportsAttachmentsResizing")]
    public bool? SupportsAttachmentsResizing { get; init; }

    [JsonPropertyName("supportsTopFeaturesQuery")]
    public bool? SupportsTopFeaturesQuery { get; init; }

    [JsonPropertyName("maxRecordCount")]
    public int? MaxRecordCount { get; init; }

    [JsonPropertyName("objectIdField")]
    public string? ObjectIdField { get; init; }

    [JsonPropertyName("fields")]
    public List<EsriFieldDto>? Fields { get; init; }

    [JsonPropertyName("relationships")]
    public List<EsriRelationshipInfoDto>? Relationships { get; init; }

    [JsonPropertyName("advancedQueryCapabilities")]
    public EsriAdvancedQueryCapabilitiesDto? AdvancedQueryCapabilities { get; init; }

    [JsonPropertyName("extent")]
    public EsriExtentDto? Extent { get; init; }
}

internal sealed class EsriAdvancedQueryCapabilitiesDto
{
    [JsonPropertyName("supportsPagination")]
    public bool? SupportsPagination { get; init; }

    [JsonPropertyName("supportsPaginationOnAggregatedQueries")]
    public bool? SupportsPaginationOnAggregatedQueries { get; init; }

    [JsonPropertyName("supportsQueryRelatedPagination")]
    public bool? SupportsQueryRelatedPagination { get; init; }

    [JsonPropertyName("supportsAdvancedQueryRelated")]
    public bool? SupportsAdvancedQueryRelated { get; init; }

    [JsonPropertyName("supportsOrderBy")]
    public bool? SupportsOrderBy { get; init; }

    [JsonPropertyName("supportsDistinct")]
    public bool? SupportsDistinct { get; init; }
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

internal sealed class EsriRelationshipInfoDto
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("relatedTableId")]
    public int? RelatedTableId { get; init; }

    [JsonPropertyName("cardinality")]
    public string? Cardinality { get; init; }

    [JsonPropertyName("role")]
    public string? Role { get; init; }

    [JsonPropertyName("keyField")]
    public string? KeyField { get; init; }

    [JsonPropertyName("composite")]
    public bool? Composite { get; init; }
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