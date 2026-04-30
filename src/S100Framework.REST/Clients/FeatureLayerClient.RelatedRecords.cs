using S100Framework.REST.Exceptions;
using S100Framework.REST.Internal.Dto;
using S100Framework.REST.Models;

namespace S100Framework.REST.Clients;

/// <summary>
/// Provides related-record query operations for <see cref="FeatureLayerClient" />.
/// </summary>
public sealed partial class FeatureLayerClient
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<RelatedRecordGroup>> QueryRelatedRecordsAsync(
        RelatedRecordsQuery query,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(query);

        query.Validate();

        await EnsureRelatedRecordsQueryCapabilitiesAsync(
            query,
            requiresAdvancedQueryRelated: !string.IsNullOrWhiteSpace(query.OrderBy),
            cancellationToken);

        var response = await _serviceClient.QueryRelatedRecordsAsync(
            _layerId,
            query,
            returnCountOnly: false,
            cancellationToken);

        var objectIdFieldName = response.Fields?
            .FirstOrDefault(field => string.Equals(field.Type, "esriFieldTypeOID", StringComparison.OrdinalIgnoreCase))
            ?.Name;

        var srid = ResolveSrid(response.SpatialReference);

        var groups = (response.RelatedRecordGroups ?? new List<EsriRelatedRecordGroupDto>())
            .Select(group => new RelatedRecordGroup(
                group.ObjectId,
                (group.RelatedRecords ?? new List<EsriFeatureDto>())
                    .Select(feature => MapRelatedRecord(
                        feature,
                        response.GeometryType,
                        srid,
                        objectIdFieldName))
                    .ToArray()))
            .ToArray();

        return groups;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RelatedRecordCountGroup>> QueryRelatedRecordCountsAsync(
        RelatedRecordsQuery query,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(query);

        query.Validate();

        await EnsureRelatedRecordsQueryCapabilitiesAsync(
            query,
            requiresAdvancedQueryRelated: true,
            cancellationToken);

        var response = await _serviceClient.QueryRelatedRecordsAsync(
            _layerId,
            query,
            returnCountOnly: true,
            cancellationToken);

        return (response.RelatedRecordGroups ?? new List<EsriRelatedRecordGroupDto>())
            .Select(static group => new RelatedRecordCountGroup(
                group.ObjectId,
                group.Count ?? 0))
            .ToArray();
    }

    private async Task EnsureRelatedRecordsQueryCapabilitiesAsync(
        RelatedRecordsQuery query,
        bool requiresAdvancedQueryRelated,
        CancellationToken cancellationToken) {
        var requiresPagination = query.ResultOffset.HasValue || query.ResultRecordCount.HasValue;

        if (!requiresAdvancedQueryRelated && !requiresPagination) {
            return;
        }

        var schema = await GetSchemaAsync(cancellationToken);

        if (requiresAdvancedQueryRelated && !schema.Capabilities.SupportsAdvancedQueryRelated) {
            throw new FeatureServiceCapabilityException(
                $"Layer '{schema.Name}' ({schema.LayerId}) does not support advanced related-record queries.");
        }

        if (requiresPagination && !schema.Capabilities.SupportsQueryRelatedPagination) {
            throw new FeatureServiceCapabilityException(
                $"Layer '{schema.Name}' ({schema.LayerId}) does not support related-record query pagination.");
        }
    }
}