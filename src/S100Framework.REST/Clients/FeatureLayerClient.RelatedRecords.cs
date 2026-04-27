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

        var response = await _serviceClient.QueryRelatedRecordsAsync(_layerId, query, cancellationToken);

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
}