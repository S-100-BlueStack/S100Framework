namespace S100Framework.REST.Models;

public sealed record FeatureStatisticsQuery
{
    public string Where { get; init; } = "1=1";

    public IReadOnlyList<StatisticDefinition> Statistics { get; init; } = Array.Empty<StatisticDefinition>();

    public IReadOnlyList<string>? GroupByFields { get; init; }

    public string? HavingClause { get; init; }

    public string? OrderBy { get; init; }

    public FeatureSpatialFilter? SpatialFilter { get; init; }

    public void Validate() {
        if (Statistics.Count == 0) {
            throw new InvalidOperationException("At least one statistic definition must be provided.");
        }

        foreach (var statistic in Statistics) {
            if (string.IsNullOrWhiteSpace(statistic.OnStatisticField)) {
                throw new InvalidOperationException("StatisticDefinition.OnStatisticField must be provided.");
            }

            if (string.IsNullOrWhiteSpace(statistic.OutStatisticFieldName)) {
                throw new InvalidOperationException("StatisticDefinition.OutStatisticFieldName must be provided.");
            }
        }
    }
}