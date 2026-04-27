using System.Globalization;
using System.Text.Json;
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

        static string MapStatisticType(StatisticType value) {
            return value switch {
                StatisticType.Count => "count",
                StatisticType.Sum => "sum",
                StatisticType.Min => "min",
                StatisticType.Max => "max",
                StatisticType.Average => "avg",
                StatisticType.StandardDeviation => "stddev",
                StatisticType.Variance => "var",
                StatisticType.PercentileContinuous => "PERCENTILE_CONT",
                StatisticType.PercentileDiscrete => "PERCENTILE_DISC",
                _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
            };
        }

        static string MapPercentileOrder(StatisticPercentileOrder value) {
            return value switch {
                StatisticPercentileOrder.Asc => "ASC",
                StatisticPercentileOrder.Desc => "DESC",
                _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
            };
        }

        var parameters = new Dictionary<string, string?> {
            ["f"] = "json",
            ["where"] = string.IsNullOrWhiteSpace(query.Where) ? "1=1" : query.Where,
            ["returnGeometry"] = "false",
            ["outStatistics"] = JsonSerializer.Serialize(
                query.Statistics.Select(statistic => {
                    var payload = new Dictionary<string, object?> {
                        ["statisticType"] = MapStatisticType(statistic.StatisticType),
                        ["onStatisticField"] = statistic.OnStatisticField,
                        ["outStatisticFieldName"] = statistic.OutStatisticFieldName
                    };

                    if (statistic.PercentileParameters is not null) {
                        payload["statisticParameters"] = new Dictionary<string, object?> {
                            ["value"] = statistic.PercentileParameters.Value,
                            ["orderBy"] = MapPercentileOrder(statistic.PercentileParameters.OrderBy)
                        };
                    }

                    return payload;
                }).ToArray()),
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