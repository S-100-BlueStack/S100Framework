namespace S100Framework.REST.Models;

public sealed record StatisticDefinition(
    string OnStatisticField,
    string OutStatisticFieldName,
    StatisticType StatisticType);