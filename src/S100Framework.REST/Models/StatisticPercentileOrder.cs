namespace S100Framework.REST.Models;

/// <summary>
/// Defines the sort order used when calculating percentile statistics.
/// </summary>
public enum StatisticPercentileOrder
{
    /// <summary>
    /// Calculates the percentile using ascending order.
    /// </summary>
    Asc = 0,

    /// <summary>
    /// Calculates the percentile using descending order.
    /// </summary>
    Desc = 1
}