using System.Text.Json;
using S100Framework.REST.Models;

namespace S100Framework.REST.Clients;

/// <summary>
/// Provides shared serialization helpers for statistics payloads used by Feature Service query operations.
/// </summary>
public sealed partial class FeatureServiceClient
{
    private enum PercentileStatisticNameFormat
    {
        UpperSql,
        LowerRest
    }

    private static string SerializeOutStatistics(
        IReadOnlyList<StatisticDefinition> statistics,
        PercentileStatisticNameFormat percentileNameFormat) {
        ArgumentNullException.ThrowIfNull(statistics);

        var payload = statistics.Select(statistic => {
            var entry = new Dictionary<string, object?> {
                ["statisticType"] = MapStatisticType(
                    statistic.StatisticType,
                    percentileNameFormat),
                ["onStatisticField"] = statistic.OnStatisticField,
                ["outStatisticFieldName"] = statistic.OutStatisticFieldName
            };

            if (statistic.PercentileParameters is not null) {
                entry["statisticParameters"] = new Dictionary<string, object?> {
                    ["value"] = statistic.PercentileParameters.Value,
                    ["orderBy"] = MapPercentileOrder(statistic.PercentileParameters.OrderBy)
                };
            }

            return entry;
        }).ToArray();

        return JsonSerializer.Serialize(payload, JsonOptions);
    }

    private static string MapStatisticType(
        StatisticType statisticType,
        PercentileStatisticNameFormat percentileNameFormat) {
        return statisticType switch {
            StatisticType.Count => "count",
            StatisticType.Sum => "sum",
            StatisticType.Min => "min",
            StatisticType.Max => "max",
            StatisticType.Average => "avg",
            StatisticType.StandardDeviation => "stddev",
            StatisticType.Variance => "var",
            StatisticType.PercentileContinuous => percentileNameFormat == PercentileStatisticNameFormat.UpperSql
                ? "PERCENTILE_CONT"
                : "percentile_cont",
            StatisticType.PercentileDiscrete => percentileNameFormat == PercentileStatisticNameFormat.UpperSql
                ? "PERCENTILE_DISC"
                : "percentile_disc",
            _ => throw new ArgumentOutOfRangeException(
                nameof(statisticType),
                statisticType,
                "Unsupported statistic type.")
        };
    }

    private static string MapPercentileOrder(StatisticPercentileOrder order) {
        return order switch {
            StatisticPercentileOrder.Asc => "ASC",
            StatisticPercentileOrder.Desc => "DESC",
            _ => throw new ArgumentOutOfRangeException(
                nameof(order),
                order,
                "Unsupported percentile order.")
        };
    }
}