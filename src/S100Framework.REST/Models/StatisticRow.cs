namespace S100Framework.REST.Models;

public sealed record StatisticRow(
    IReadOnlyDictionary<string, object?> Attributes) : IAttributeRecord;