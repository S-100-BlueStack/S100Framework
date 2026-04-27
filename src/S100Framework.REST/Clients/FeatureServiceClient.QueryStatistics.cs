using System.Globalization;
using S100Framework.REST.Internal.Dto;
using S100Framework.REST.Models;

namespace S100Framework.REST.Clients;

/// <summary>
/// Provides statistics query operations for feature service layer endpoints.
/// </summary>
public sealed partial class FeatureServiceClient
{
    internal Task<EsriQueryResponseDto> QueryStatisticsAsync(
        int layerId,
        FeatureStatisticsQuery query,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(query);

        query.Validate();

        var parameters = new Dictionary<string, string?> {
            ["f"] = "json",
            ["where"] = string.IsNullOrWhiteSpace(query.Where) ? "1=1" : query.Where,
            ["returnGeometry"] = "false",
            ["outStatistics"] = SerializeOutStatistics(
                query.Statistics,
                PercentileStatisticNameFormat.UpperSql),
            ["groupByFieldsForStatistics"] = query.GroupByFields is { Count: > 0 }
                ? string.Join(",", query.GroupByFields)
                : null,
            ["havingClause"] = query.HavingClause,
            ["orderByFields"] = query.OrderBy
        };

        if (query.ResultOffset.HasValue) {
            parameters["resultOffset"] = query.ResultOffset.Value.ToString(CultureInfo.InvariantCulture);
        }

        if (query.ResultRecordCount.HasValue) {
            parameters["resultRecordCount"] = query.ResultRecordCount.Value.ToString(CultureInfo.InvariantCulture);
        }

        ApplySpatialFilter(parameters, query.SpatialFilter);

        return SendLayerQueryAsync<EsriQueryResponseDto>(
            $"{layerId.ToString(CultureInfo.InvariantCulture)}/query",
            parameters,
            cancellationToken);
    }
}