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
    /// Gets the number of aggregated rows to skip before returning results.
    /// </summary>
    /// <remarks>
    /// This only applies to grouped statistics queries.
    /// </remarks>
    public int? ResultOffset { get; init; }

    /// <summary>
    /// Gets the maximum number of aggregated rows to return.
    /// </summary>
    /// <remarks>
    /// This only applies to grouped statistics queries.
    /// </remarks>
    public int? ResultRecordCount { get; init; }

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

        var statisticAliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var statistic in Statistics) {
            if (string.IsNullOrWhiteSpace(statistic.OnStatisticField)) {
                throw new InvalidOperationException("StatisticDefinition.OnStatisticField must be provided.");
            }

            if (string.IsNullOrWhiteSpace(statistic.OutStatisticFieldName)) {
                throw new InvalidOperationException("StatisticDefinition.OutStatisticFieldName must be provided.");
            }

            // Statistics are returned as keyed attributes. Duplicate aliases would silently overwrite values.
            if (!statisticAliases.Add(statistic.OutStatisticFieldName)) {
                throw new InvalidOperationException(
                    $"Duplicate statistic alias '{statistic.OutStatisticFieldName}' is not allowed.");
            }
        }

        if (GroupByFields is { Count: 0 }) {
            throw new InvalidOperationException("GroupByFields must not be empty when provided.");
        }

        if (GroupByFields is { Count: > 0 }) {
            foreach (var groupByField in GroupByFields) {
                if (string.IsNullOrWhiteSpace(groupByField)) {
                    throw new InvalidOperationException("GroupByFields must not contain empty values.");
                }
            }
        }

        if (HavingClause is not null) {
            if (string.IsNullOrWhiteSpace(HavingClause)) {
                throw new InvalidOperationException("HavingClause must not be empty when provided.");
            }

            if (GroupByFields is not { Count: > 0 }) {
                throw new InvalidOperationException(
                    "HavingClause requires at least one GroupByFields entry.");
            }
        }

        if (OrderBy is not null && string.IsNullOrWhiteSpace(OrderBy)) {
            throw new InvalidOperationException("OrderBy must not be empty when provided.");
        }

        if (ResultOffset is < 0) {
            throw new InvalidOperationException("ResultOffset must be greater than or equal to zero when provided.");
        }

        if (ResultRecordCount is <= 0) {
            throw new InvalidOperationException("ResultRecordCount must be greater than zero when provided.");
        }

        if ((ResultOffset.HasValue || ResultRecordCount.HasValue) &&
            GroupByFields is not { Count: > 0 }) {
            throw new InvalidOperationException(
                "ResultOffset and ResultRecordCount require at least one GroupByFields entry.");
        }
    }
}