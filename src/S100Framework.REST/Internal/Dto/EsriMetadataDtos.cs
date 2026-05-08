using System.Text.Json.Serialization;
using S100Framework.REST.Internal.Json;

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

    [JsonPropertyName("syncEnabled")]
    public bool? SyncEnabled { get; init; }

    [JsonPropertyName("supportsAppend")]
    public bool? SupportsAppend { get; init; }

    [JsonPropertyName("supportedAppendFormats")]
    [JsonConverter(typeof(EsriStringListJsonConverter))]
    public List<string>? SupportedAppendFormats { get; init; }

    [JsonPropertyName("advancedEditingCapabilities")]
    public EsriAdvancedEditingCapabilitiesDto? AdvancedEditingCapabilities { get; init; }

    [JsonPropertyName("extractChangesCapabilities")]
    public EsriExtractChangesCapabilitiesDto? ExtractChangesCapabilities { get; init; }

    [JsonPropertyName("supportsQueryDomains")]
    public bool? SupportsQueryDomains { get; init; }

    [JsonPropertyName("supportsQueryDataElements")]
    public bool? SupportsQueryDataElements { get; init; }

    [JsonPropertyName("supportsQueryContingentValues")]
    public bool? SupportsQueryContingentValues { get; init; }

    [JsonPropertyName("supportedContingentValuesFormats")]
    public string? SupportedContingentValuesFormats { get; init; }

    [JsonPropertyName("supportsContingentValuesJson")]
    public int? SupportsContingentValuesJson { get; init; }

    [JsonPropertyName("supportsRelationshipsResource")]
    public bool? SupportsRelationshipsResource { get; init; }
}

internal sealed class EsriDatasetDto
{
    [JsonPropertyName("id")]
    public int? Id { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }
}

internal sealed class EsriLayerMetadataDto
{
    [JsonPropertyName("id")]
    public int? Id { get; init; }

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

    [JsonPropertyName("advancedEditingCapabilities")]
    public EsriAdvancedEditingCapabilitiesDto? AdvancedEditingCapabilities { get; init; }

    [JsonPropertyName("extent")]
    public EsriExtentDto? Extent { get; init; }

    [JsonPropertyName("uniqueIdInfo")]
    public EsriUniqueIdInfoDto? UniqueIdInfo { get; init; }

    [JsonPropertyName("supportsAppend")]
    public bool? SupportsAppend { get; init; }

    [JsonPropertyName("supportedAppendFormats")]
    [JsonConverter(typeof(EsriStringListJsonConverter))]
    public List<string>? SupportedAppendFormats { get; init; }

    [JsonPropertyName("supportedAppendSourceFilterFormats")]
    [JsonConverter(typeof(EsriStringListJsonConverter))]
    public List<string>? SupportedAppendSourceFilterFormats { get; init; }

    [JsonPropertyName("supportedAppendCapabilities")]
    public string? SupportedAppendCapabilities { get; init; }

    [JsonPropertyName("hasContingentValuesDefinition")]
    public bool? HasContingentValuesDefinition { get; init; }

    [JsonPropertyName("advancedQueryAnalyticCapabilities")]
    public EsriAdvancedQueryAnalyticCapabilitiesDto? AdvancedQueryAnalyticCapabilities { get; init; }

    [JsonPropertyName("supportsCalculate")]
    public bool? SupportsCalculate { get; init; }

    [JsonPropertyName("supportsAsyncCalculate")]
    public bool? SupportsAsyncCalculate { get; init; }

    [JsonPropertyName("supportsValidateSQL")]
    public bool? SupportsValidateSql { get; init; }
}

internal sealed class EsriUniqueIdInfoDto
{
    [JsonPropertyName("type")]
    public string? Type { get; init; }

    [JsonPropertyName("fields")]
    public IReadOnlyList<string>? Fields { get; init; }

    [JsonPropertyName("OIDFieldContainsHashValue")]
    public bool? OidFieldContainsHashValue { get; init; }
}

internal sealed class EsriAdvancedEditingCapabilitiesDto
{
    [JsonPropertyName("supportsAsyncApplyEdits")]
    public bool? SupportsAsyncApplyEdits { get; init; }

    [JsonPropertyName("supportedSqlFormatsInCalculate")]
    public List<string>? SupportedSqlFormatsInCalculate { get; init; }

    [JsonPropertyName("supportedSqlFormatesInCalculate")]
    public List<string>? SupportedSqlFormatesInCalculate { get; init; }
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

    [JsonPropertyName("supportsReturningGeometryEnvelope")]
    public bool? SupportsReturningGeometryEnvelope { get; init; }

    [JsonPropertyName("supportsFullTextSearch")]
    public bool? SupportsFullTextSearch { get; init; }

    [JsonPropertyName("supportsPercentileStatistics")]
    public bool? SupportsPercentileStatistics { get; init; }

    [JsonPropertyName("supportsQueryDateBins")]
    public bool? SupportsQueryDateBins { get; init; }

    [JsonPropertyName("supportsQueryAnalytic")]
    public bool? SupportsQueryAnalytic { get; init; }

    [JsonPropertyName("supportsReturningQueryExtent")]
    public bool? SupportsReturningQueryExtent { get; init; }

    [JsonPropertyName("supportsReturningGeometryCentroid")]
    public bool? SupportsReturningGeometryCentroid { get; init; }

    [JsonPropertyName("supportsDefaultSR")]
    public bool? SupportsDefaultSrid { get; init; }

    [JsonPropertyName("supportsOutFieldSqlExpression")]
    public bool? SupportsOutFieldSqlExpression { get; init; }

    [JsonPropertyName("supportsSqlExpression")]
    public bool? SupportsSqlExpression { get; init; }

    [JsonPropertyName("supportsHavingClause")]
    public bool? SupportsHavingClause { get; init; }

    [JsonPropertyName("supportsQueryWithDistance")]
    public bool? SupportsQueryWithDistance { get; init; }

    [JsonPropertyName("supportsQueryWithResultType")]
    public bool? SupportsQueryWithResultType { get; init; }

    [JsonPropertyName("supportsQueryWithHistoricMoment")]
    public bool? SupportsQueryWithHistoricMoment { get; init; }

    [JsonPropertyName("supportsQueryWithDatumTransformation")]
    public bool? SupportsQueryWithDatumTransformation { get; init; }

    [JsonPropertyName("supportsCoordinatesQuantization")]
    public bool? SupportsCoordinatesQuantization { get; init; }

    [JsonPropertyName("supportsCurrentUserQueries")]
    public bool? SupportsCurrentUserQueries { get; init; }

    [JsonPropertyName("supportsQueryWithCacheHint")]
    public bool? SupportsQueryWithCacheHint { get; init; }

    [JsonPropertyName("supportsQueryAttachmentsCountOnly")]
    public bool? SupportsQueryAttachmentsCountOnly { get; init; }

    [JsonPropertyName("supportsQueryAttachmentOrderByFields")]
    public bool? SupportsQueryAttachmentOrderByFields { get; init; }
}

internal sealed class EsriAdvancedQueryAnalyticCapabilitiesDto
{
    [JsonPropertyName("supportsAsync")]
    public bool? SupportsAsync { get; init; }

    [JsonPropertyName("supportsLinearRegression")]
    public bool? SupportsLinearRegression { get; init; }

    [JsonPropertyName("supportsPercentileAnalytic")]
    public bool? SupportsPercentileAnalytic { get; init; }
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
    public int? Id { get; init; }

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

internal sealed class EsriExtractChangesCapabilitiesDto
{
    [JsonPropertyName("supportsReturnIdsOnly")]
    public bool? SupportsReturnIdsOnly { get; init; }

    [JsonPropertyName("supportsReturnExtentOnly")]
    public bool? SupportsReturnExtentOnly { get; init; }

    [JsonPropertyName("supportsReturnAttachments")]
    public bool? SupportsReturnAttachments { get; init; }

    [JsonPropertyName("supportsLayerQueries")]
    public bool? SupportsLayerQueries { get; init; }

    [JsonPropertyName("supportsGeometry")]
    public bool? SupportsGeometry { get; init; }

    [JsonPropertyName("supportsReturnFeature")]
    public bool? SupportsReturnFeature { get; init; }

    [JsonPropertyName("supportsFieldsToCompare")]
    public bool? SupportsFieldsToCompare { get; init; }

    [JsonPropertyName("supportsServerGens")]
    public bool? SupportsServerGens { get; init; }

    [JsonPropertyName("supportsReturnHasGeometryUpdates")]
    public bool? SupportsReturnHasGeometryUpdates { get; init; }
}