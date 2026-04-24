namespace S100Framework.REST.Models;

/// <summary>
/// Defines percentile-specific parameters for a statistics query.
/// </summary>
/// <param name="Value">
/// The percentile value to calculate. Valid values are between <c>0</c> and <c>1</c>.
/// </param>
/// <param name="OrderBy">
/// The sort order used for the percentile calculation.
/// </param>
public sealed record StatisticPercentileParameters(
    double Value,
    StatisticPercentileOrder OrderBy = StatisticPercentileOrder.Asc);