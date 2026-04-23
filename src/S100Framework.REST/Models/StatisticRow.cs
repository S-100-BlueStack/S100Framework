namespace S100Framework.REST.Models;

/// <summary>
/// Represents one row returned from a statistics query.
/// </summary>
/// <param name="Attributes">
/// The statistic and grouping values returned for the row.
/// </param>
public sealed record StatisticRow(
    IReadOnlyDictionary<string, object?> Attributes) : IAttributeRecord;