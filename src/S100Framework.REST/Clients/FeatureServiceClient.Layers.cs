using System.Globalization;
using S100Framework.REST.Abstractions;
using S100Framework.REST.Internal.Dto;
using S100Framework.REST.Internal.Http;
using S100Framework.REST.Models;

namespace S100Framework.REST.Clients;

/// <summary>
/// Provides layer and table resolution operations for feature service root endpoints.
/// </summary>
public sealed partial class FeatureServiceClient
{
    /// <inheritdoc />
    public IFeatureLayerClient GetLayerClient(int layerId) {
        if (layerId < 0) {
            throw new ArgumentOutOfRangeException(nameof(layerId));
        }

        return new FeatureLayerClient(this, layerId);
    }

    /// <inheritdoc />
    public async Task<IFeatureLayerClient> GetLayerClientAsync(
        string layerName,
        CancellationToken cancellationToken = default) {
        if (string.IsNullOrWhiteSpace(layerName)) {
            throw new ArgumentException("Layer name must be provided.", nameof(layerName));
        }

        var metadata = await GetMetadataAsync(cancellationToken);

        var matches = metadata.Layers
            .Concat(metadata.Tables)
            .Where(dataset => string.Equals(dataset.Name, layerName, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        return matches.Length switch {
            0 => throw new InvalidOperationException(
                $"No layer or table named '{layerName}' was found in the feature service."),
            > 1 => throw new InvalidOperationException(
                $"Multiple layers or tables named '{layerName}' were found in the feature service. Use the layer ID instead."),
            _ => GetLayerClient(matches[0].Id)
        };
    }

    internal async Task<FeatureLayerSchema> GetLayerSchemaAsync(
        int layerId,
        CancellationToken cancellationToken = default) {
        var uri = UriUtility.WithQuery(
            UriUtility.AppendPath(_serviceUri, layerId.ToString(CultureInfo.InvariantCulture)),
            new Dictionary<string, string?> {
                ["f"] = "json"
            });

        var dto = await GetAsync<EsriLayerMetadataDto>(uri, cancellationToken);

        var srid = dto.Extent?.SpatialReference is null
            ? null
            : _options.PreferLatestWkid
                ? dto.Extent.SpatialReference.LatestWkid ?? dto.Extent.SpatialReference.Wkid
                : dto.Extent.SpatialReference.Wkid ?? dto.Extent.SpatialReference.LatestWkid;

        var supportsPagination = dto.AdvancedQueryCapabilities?.SupportsPagination ?? false;

        var uniqueIdInfo = dto.UniqueIdInfo is null
            ? null
            : new FeatureLayerUniqueIdInfo(
                dto.UniqueIdInfo.Type ?? "unknown",
                dto.UniqueIdInfo.Fields?
                    .Where(static field => !string.IsNullOrWhiteSpace(field))
                    .ToArray() ?? Array.Empty<string>(),
                dto.UniqueIdInfo.OidFieldContainsHashValue ?? false);

        var capabilities = new FeatureLayerCapabilities(
            dto.HasAttachments ?? false,
            dto.SupportsQueryAttachments ?? false,
            dto.SupportsAttachmentsResizing ?? false,
            dto.SupportsTopFeaturesQuery ?? false,
            supportsPagination,
            dto.AdvancedQueryCapabilities?.SupportsPaginationOnAggregatedQueries ?? false,
            dto.AdvancedQueryCapabilities?.SupportsQueryRelatedPagination ?? false,
            dto.AdvancedQueryCapabilities?.SupportsAdvancedQueryRelated ?? false,
            dto.AdvancedQueryCapabilities?.SupportsOrderBy ?? false,
            dto.AdvancedQueryCapabilities?.SupportsDistinct ?? false,
            dto.AdvancedEditingCapabilities?.SupportsAsyncApplyEdits ?? false,
            dto.AdvancedQueryCapabilities?.SupportsReturningGeometryEnvelope ?? false,
            dto.AdvancedQueryCapabilities?.SupportsFullTextSearch ?? false,
            dto.AdvancedQueryCapabilities?.SupportsPercentileStatistics ?? false,
            dto.SupportsAppend ?? false,
            dto.AdvancedQueryCapabilities?.SupportsQueryDateBins ?? false,
            dto.AdvancedQueryCapabilities?.SupportsQueryAnalytic ?? false,
            dto.AdvancedQueryAnalyticCapabilities?.SupportsAsync ?? false,
            dto.SupportsCalculate ?? false,
            dto.SupportsAsyncCalculate ?? false,
            dto.AdvancedQueryCapabilities?.SupportsReturningQueryExtent ?? false,
            dto.AdvancedQueryCapabilities?.SupportsReturningGeometryCentroid ?? false,
            dto.AdvancedQueryCapabilities?.SupportsDefaultSrid ?? false,
            dto.AdvancedQueryCapabilities?.SupportsOutFieldSqlExpression ?? false,
            dto.AdvancedQueryCapabilities?.SupportsSqlExpression ?? false,
            dto.AdvancedQueryCapabilities?.SupportsHavingClause ?? false,
            dto.AdvancedQueryCapabilities?.SupportsQueryWithDistance ?? false,
            dto.AdvancedQueryCapabilities?.SupportsQueryWithResultType ?? false,
            dto.AdvancedQueryCapabilities?.SupportsQueryWithHistoricMoment ?? false,
            dto.AdvancedQueryCapabilities?.SupportsQueryWithDatumTransformation ?? false,
            dto.AdvancedQueryCapabilities?.SupportsCoordinatesQuantization ?? false,
            dto.AdvancedQueryCapabilities?.SupportsCurrentUserQueries ?? false,
            dto.AdvancedQueryCapabilities?.SupportsQueryWithCacheHint ?? false,
            dto.AdvancedQueryCapabilities?.SupportsQueryAttachmentsCountOnly ?? false,
            dto.AdvancedQueryCapabilities?.SupportsQueryAttachmentOrderByFields ?? false) {
            SupportedSqlFormatsInCalculate = MapSupportedCalculateSqlFormats(dto.AdvancedEditingCapabilities),
            SupportsValidateSql = dto.SupportsValidateSql ?? false
        };

        return new FeatureLayerSchema(
            dto.Id,
            dto.Name ?? $"Layer {dto.Id}",
            dto.GeometryType,
            srid,
            dto.HasZ ?? false,
            dto.HasM ?? false,
            dto.MaxRecordCount,
            dto.ObjectIdField,
            dto.Fields?.Select(MapField).ToArray() ?? Array.Empty<FeatureField>(),
            capabilities,
            dto.Relationships?.Select(MapRelationship).ToArray() ?? Array.Empty<FeatureRelationshipInfo>()) {
            UniqueIdInfo = uniqueIdInfo,
            SupportedAppendFormats = dto.SupportedAppendFormats?
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .ToArray() ?? Array.Empty<string>(),
            SupportedAppendSourceFilterFormats = dto.SupportedAppendSourceFilterFormats?
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .ToArray() ?? Array.Empty<string>(),
            SupportedAppendCapabilities = dto.SupportedAppendCapabilities,
            HasContingentValuesDefinition = dto.HasContingentValuesDefinition ?? false
        };
    }

    private static IReadOnlyList<FeatureQuerySqlFormat> MapSupportedCalculateSqlFormats(
        EsriAdvancedEditingCapabilitiesDto? dto) {
        if (dto is null) {
            return Array.Empty<FeatureQuerySqlFormat>();
        }

        var values = Enumerable.Empty<string>();

        if (dto.SupportedSqlFormatsInCalculate is not null) {
            values = values.Concat(dto.SupportedSqlFormatsInCalculate);
        }

        if (dto.SupportedSqlFormatesInCalculate is not null) {
            values = values.Concat(dto.SupportedSqlFormatesInCalculate);
        }

        return values
            .Select(MapCalculateSqlFormat)
            .Where(static value => value != FeatureQuerySqlFormat.None)
            .Distinct()
            .ToArray();
    }

    private static FeatureQuerySqlFormat MapCalculateSqlFormat(string? value) {
        return value?.Trim().ToLowerInvariant() switch {
            "standard" => FeatureQuerySqlFormat.Standard,
            "native" => FeatureQuerySqlFormat.Native,
            _ => FeatureQuerySqlFormat.None
        };
    }

    private static FeatureField MapField(EsriFieldDto dto) {
        return new FeatureField(
            dto.Name ?? string.Empty,
            dto.Type ?? string.Empty,
            dto.Alias,
            dto.Nullable ?? true,
            dto.Length);
    }

    private static FeatureRelationshipInfo MapRelationship(EsriRelationshipInfoDto dto) {
        return new FeatureRelationshipInfo(
            dto.Id,
            dto.Name,
            dto.RelatedTableId,
            dto.Cardinality,
            dto.Role,
            dto.KeyField,
            dto.Composite);
    }
}