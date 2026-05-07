using System.Text.Json;
using S100Framework.REST.Models;

namespace S100Framework.REST.Internal.Validation;

internal static class QueryBinRequestValidation
{
    internal static void ValidateJsonObject(
        string? json,
        string propertyName,
        bool required) {
        if (json is null) {
            if (required) {
                throw new InvalidOperationException($"{propertyName} must be provided.");
            }

            return;
        }

        if (string.IsNullOrWhiteSpace(json)) {
            throw new InvalidOperationException($"{propertyName} must not be empty when provided.");
        }

        try {
            using var document = JsonDocument.Parse(json);

            if (document.RootElement.ValueKind != JsonValueKind.Object) {
                throw new InvalidOperationException($"{propertyName} must be a JSON object.");
            }
        }
        catch (JsonException exception) {
            throw new InvalidOperationException($"{propertyName} must contain valid JSON.", exception);
        }
    }

    internal static void ValidateBinOrder(QueryBinsOrder? binOrder) {
        if (binOrder.HasValue && !Enum.IsDefined(binOrder.Value)) {
            throw new InvalidOperationException("BinOrder must be a supported bin order.");
        }
    }

    internal static void ValidateStatistics(
        IReadOnlyList<StatisticDefinition?>? statistics,
        bool required) {
        if (statistics is null) {
            if (required) {
                throw new InvalidOperationException("At least one statistic must be provided.");
            }

            return;
        }

        if (statistics.Count == 0) {
            throw new InvalidOperationException(
                required
                    ? "At least one statistic must be provided."
                    : "Statistics must not be empty when provided.");
        }

        var aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var statistic in statistics) {
            if (statistic is null) {
                throw new InvalidOperationException("Statistics must not contain null values.");
            }

            if (string.IsNullOrWhiteSpace(statistic.OnStatisticField)) {
                throw new InvalidOperationException("StatisticDefinition.OnStatisticField must be provided.");
            }

            if (string.IsNullOrWhiteSpace(statistic.OutStatisticFieldName)) {
                throw new InvalidOperationException("StatisticDefinition.OutStatisticFieldName must be provided.");
            }

            if (!Enum.IsDefined(statistic.StatisticType)) {
                throw new InvalidOperationException("StatisticDefinition.StatisticType must be a supported statistic type.");
            }

            if (!aliases.Add(statistic.OutStatisticFieldName)) {
                throw new InvalidOperationException(
                    $"Duplicate statistic alias '{statistic.OutStatisticFieldName}' is not allowed.");
            }

            var isPercentile = statistic.StatisticType is
                StatisticType.PercentileContinuous or
                StatisticType.PercentileDiscrete;

            if (isPercentile) {
                if (statistic.PercentileParameters is null) {
                    throw new InvalidOperationException(
                        "Percentile statistics require PercentileParameters to be provided.");
                }

                if (double.IsNaN(statistic.PercentileParameters.Value) ||
                    double.IsInfinity(statistic.PercentileParameters.Value) ||
                    statistic.PercentileParameters.Value < 0d ||
                    statistic.PercentileParameters.Value > 1d) {
                    throw new InvalidOperationException(
                        "PercentileParameters.Value must be between 0 and 1.");
                }

                if (!Enum.IsDefined(statistic.PercentileParameters.OrderBy)) {
                    throw new InvalidOperationException(
                        "PercentileParameters.OrderBy must be a supported percentile order.");
                }
            }
            else if (statistic.PercentileParameters is not null) {
                throw new InvalidOperationException(
                    "PercentileParameters can only be used with percentile statistic types.");
            }
        }
    }
}