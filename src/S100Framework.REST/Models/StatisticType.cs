namespace S100Framework.REST.Models;

/// <summary>
/// Defines the aggregate function used in a statistics query.
/// </summary>
public enum StatisticType
{
    /// <summary>
    /// Counts the number of matching rows.
    /// </summary>
    Count,

    /// <summary>
    /// Returns the sum of the matching values.
    /// </summary>
    Sum,

    /// <summary>
    /// Returns the minimum matching value.
    /// </summary>
    Min,

    /// <summary>
    /// Returns the maximum matching value.
    /// </summary>
    Max,

    /// <summary>
    /// Returns the average of the matching values.
    /// </summary>
    Average,

    /// <summary>
    /// Returns the standard deviation of the matching values.
    /// </summary>
    StandardDeviation,

    /// <summary>
    /// Returns the variance of the matching values.
    /// </summary>
    Variance
}