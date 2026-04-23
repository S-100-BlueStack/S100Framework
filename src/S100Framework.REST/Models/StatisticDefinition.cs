namespace S100Framework.REST.Models;

/// <summary>
/// Defines a single aggregate statistic to compute in a statistics query.
/// </summary>
/// <param name="OnStatisticField">
/// The source field to aggregate.
/// </param>
/// <param name="OutStatisticFieldName">
/// The output field name assigned to the computed statistic.
/// </param>
/// <param name="StatisticType">
/// The aggregate function to apply.
/// </param>
public sealed record StatisticDefinition(
    string OnStatisticField,
    string OutStatisticFieldName,
    StatisticType StatisticType);