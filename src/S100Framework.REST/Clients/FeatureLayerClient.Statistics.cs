using S100Framework.REST.Exceptions;
using S100Framework.REST.Internal.Dto;
using S100Framework.REST.Models;

namespace S100Framework.REST.Clients;

/// <summary>
/// Provides statistics query operations for <see cref="FeatureLayerClient" />.
/// </summary>
public sealed partial class FeatureLayerClient
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<StatisticRow>> QueryStatisticsAsync(
        FeatureStatisticsQuery query,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(query);

        query.Validate();

        var requiresAggregatedPaginationCheck =
            query.ResultOffset.HasValue || query.ResultRecordCount.HasValue;

        var requiresPercentileCapabilityCheck = query.Statistics.Any(static statistic =>
            statistic.StatisticType is
                StatisticType.PercentileContinuous or
                StatisticType.PercentileDiscrete);

        FeatureLayerSchema? schema = null;

        if (requiresAggregatedPaginationCheck || requiresPercentileCapabilityCheck) {
            schema = await GetSchemaAsync(cancellationToken);
        }

        if (requiresAggregatedPaginationCheck &&
            !schema!.Capabilities.SupportsPaginationOnAggregatedQueries) {
            throw new FeatureServiceCapabilityException(
                $"Layer '{schema.Name}' ({schema.LayerId}) does not support pagination on aggregated queries.");
        }

        if (requiresPercentileCapabilityCheck &&
            !schema!.Capabilities.SupportsPercentileStatistics) {
            throw new FeatureServiceCapabilityException(
                $"Layer '{schema.Name}' ({schema.LayerId}) does not support percentile statistics.");
        }

        var response = await _serviceClient.QueryStatisticsAsync(_layerId, query, cancellationToken);

        return (response.Features ?? Enumerable.Empty<EsriFeatureDto?>())
            .Where(static feature => feature is not null)
            .Select(static feature => new StatisticRow(ReadAttributes(feature!.Attributes)))
            .ToArray();
    }
}