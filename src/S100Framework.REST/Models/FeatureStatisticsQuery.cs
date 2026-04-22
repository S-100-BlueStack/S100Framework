namespace S100Framework.REST.Models;

/// <summary>
/// Defines a statistics query for a layer or table.
/// </summary>
public sealed record FeatureStatisticsQuery
{
    /// <summary>
    /// Gets the SQL where clause used to filter records before aggregation.
    /// </summary>
    /// <remarks>
    /// Defaults to <c>1=1</c>, which matches all records.
    /// </remarks>
    public string Where { get; init; } = "1=1";

    /// <summary>
    /// Gets the statistic definitions to compute.
    /// </summary>
    /// <remarks>
    /// At least one statistic definition must be provided.
    /// </remarks>
    public IReadOnlyList<StatisticDefinition> Statistics { get; init; } = Array.Empty<StatisticDefinition>();

    /// <summary>
    /// Gets the fields used for group-by aggregation.
    /// </summary>
    public IReadOnlyList<string>? GroupByFields { get; init; }

    /// <summary>
    /// Gets the optional HAVING clause applied after grouping.
    /// </summary>
    public string? HavingClause { get; init; }

    /// <summary>
    /// Gets the optional order-by clause for the aggregated rows.
    /// </summary>
    public string? OrderBy { get; init; }

    /// <summary>
    /// Gets the optional spatial filter applied before aggregation.
    /// </summary>
    public FeatureSpatialFilter? SpatialFilter { get; init; }

    /// <summary>
    /// Validates the query configuration.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the query does not contain at least one valid statistic definition.
    /// </exception>
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