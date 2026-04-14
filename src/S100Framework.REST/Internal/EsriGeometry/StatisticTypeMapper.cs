using S100Framework.REST.Models;

namespace S100Framework.REST.Internal.EsriGeometry;

internal static class StatisticTypeMapper
{
    public static string ToEsriValue(StatisticType statisticType) {
        return statisticType switch {
            StatisticType.Count => "count",
            StatisticType.Sum => "sum",
            StatisticType.Min => "min",
            StatisticType.Max => "max",
            StatisticType.Average => "avg",
            StatisticType.StandardDeviation => "stddev",
            StatisticType.Variance => "var",
            _ => throw new ArgumentOutOfRangeException(nameof(statisticType), statisticType, null)
        };
    }
}