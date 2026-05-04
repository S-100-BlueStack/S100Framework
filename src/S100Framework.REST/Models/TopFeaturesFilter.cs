namespace S100Framework.REST.Models;

/// <summary>
/// Defines the grouping, ordering, and row limit for a top-features query.
/// </summary>
public sealed record TopFeaturesFilter
{
    /// <summary>
    /// Gets the fields used to group candidate records before selecting top rows.
    /// </summary>
    public IReadOnlyList<string> GroupByFields { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets the fields used to rank rows within each group.
    /// </summary>
    public IReadOnlyList<string> OrderByFields { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets the maximum number of rows to return per group.
    /// </summary>
    public int TopCount { get; init; }

    /// <summary>
    /// Validates the filter configuration.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when group-by fields, order-by fields, or top count are not configured correctly.
    /// </exception>
    public void Validate()
    {
        if (GroupByFields is not { Count: > 0 })
        {
            throw new InvalidOperationException("At least one group-by field must be provided.");
        }

        if (GroupByFields.Any(static field => string.IsNullOrWhiteSpace(field)))
        {
            throw new InvalidOperationException(
                "GroupByFields must not contain null, empty, or whitespace-only values.");
        }

        if (OrderByFields is not { Count: > 0 })
        {
            throw new InvalidOperationException("At least one order-by field must be provided.");
        }

        if (OrderByFields.Any(static field => string.IsNullOrWhiteSpace(field)))
        {
            throw new InvalidOperationException(
                "OrderByFields must not contain null, empty, or whitespace-only values.");
        }

        if (TopCount <= 0)
        {
            throw new InvalidOperationException("TopCount must be greater than zero.");
        }
    }
}