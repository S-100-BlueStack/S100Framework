namespace S100Framework.REST.Models;

/// <summary>
/// Defines the order used for bin results returned by a <c>queryBins</c> operation.
/// </summary>
public enum QueryBinsOrder
{
    /// <summary>
    /// Returns bin results in ascending order.
    /// </summary>
    Ascending = 0,

    /// <summary>
    /// Returns bin results in descending order.
    /// </summary>
    Descending = 1
}