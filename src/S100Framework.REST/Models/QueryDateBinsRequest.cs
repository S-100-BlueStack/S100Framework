using System.Text.Json;

namespace S100Framework.REST.Models;

/// <summary>
/// Represents a layer-level <c>queryDateBins</c> request.
/// </summary>
public sealed record QueryDateBinsRequest
{
    /// <summary>
    /// Gets the date field used to determine which bin each feature falls into.
    /// </summary>
    public string BinField { get; init; } = string.Empty;

    /// <summary>
    /// Gets the raw JSON object sent through the required <c>bin</c> parameter.
    /// </summary>
    /// <remarks>
    /// Raw JSON is used intentionally because ArcGIS supports both calendar bins and fixed bins with optional timezone
    /// and offset settings.
    /// </remarks>
    public string BinJson { get; init; } = string.Empty;

    /// <summary>
    /// Gets the statistics calculated for each date bin.
    /// </summary>
    public IReadOnlyList<StatisticDefinition> Statistics { get; init; } = Array.Empty<StatisticDefinition>();

    /// <summary>
    /// Gets the SQL WHERE clause used to filter records before binning.
    /// </summary>
    /// <remarks>
    /// Defaults to <c>1=1</c>, which matches all records.
    /// </remarks>
    public string Where { get; init; } = "1=1";

    /// <summary>
    /// Gets the optional time extent used to constrain the date-bin range.
    /// </summary>
    public FeatureTimeExtent? TimeExtent { get; init; }

    /// <summary>
    /// Gets the optional bin ordering.
    /// </summary>
    public QueryBinsOrder? BinOrder { get; init; }

    /// <summary>
    /// Gets a value indicating whether the service should return aggregate centroid information for each bin.
    /// </summary>
    public bool? ReturnCentroid { get; init; }

    /// <summary>
    /// Gets the output spatial reference used by returned centroid geometry.
    /// </summary>
    public int? OutSrid { get; init; }

    /// <summary>
    /// Gets the raw quantization parameters JSON object used for returned centroid geometry.
    /// </summary>
    public string? QuantizationParametersJson { get; init; }

    /// <summary>
    /// Gets the result offset used for aggregated-query pagination.
    /// </summary>
    public int? ResultOffset { get; init; }

    /// <summary>
    /// Gets the maximum number of date-bin rows to return.
    /// </summary>
    public int? ResultRecordCount { get; init; }

    /// <summary>
    /// Gets a value indicating whether features should be returned when the response exceeds the transfer limit.
    /// </summary>
    public bool? ReturnExceededLimitFeatures { get; init; }

    /// <summary>
    /// Gets the optional alias used for the boundary attribute returned by the service.
    /// </summary>
    public string? BinBoundaryAlias { get; init; }

    /// <summary>
    /// Gets the optional spatial filter applied before binning.
    /// </summary>
    public FeatureSpatialFilter? SpatialFilter { get; init; }

    /// <summary>
    /// Validates the date-bin query request before it is sent.
    /// </summary>
    public void Validate() {
        if (string.IsNullOrWhiteSpace(BinField)) {
            throw new InvalidOperationException("BinField must be provided.");
        }

        ValidateJsonObject(BinJson, nameof(BinJson), required: true);
        ValidateJsonObject(QuantizationParametersJson, nameof(QuantizationParametersJson), required: false);

        if (Statistics.Count == 0) {
            throw new InvalidOperationException("At least one statistic must be provided.");
        }

        if (Where is not null && string.IsNullOrWhiteSpace(Where)) {
            throw new InvalidOperationException("Where must not be empty when provided.");
        }

        if (TimeExtent is not null) {
            if (!TimeExtent.Start.HasValue && !TimeExtent.End.HasValue) {
                throw new InvalidOperationException("TimeExtent must specify at least one bound.");
            }

            if (TimeExtent.Start.HasValue &&
                TimeExtent.End.HasValue &&
                TimeExtent.Start.Value > TimeExtent.End.Value) {
                throw new InvalidOperationException(
                    "TimeExtent.Start must be less than or equal to TimeExtent.End when both are provided.");
            }
        }

        if (OutSrid is <= 0) {
            throw new InvalidOperationException("OutSrid must be greater than zero when provided.");
        }

        if (ResultOffset is < 0) {
            throw new InvalidOperationException("ResultOffset must not be negative when provided.");
        }

        if (ResultRecordCount is <= 0) {
            throw new InvalidOperationException("ResultRecordCount must be greater than zero when provided.");
        }

        if (BinBoundaryAlias is not null && string.IsNullOrWhiteSpace(BinBoundaryAlias)) {
            throw new InvalidOperationException("BinBoundaryAlias must not be empty when provided.");
        }

        ValidateStatistics();
    }

    private void ValidateStatistics() {
        var aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var statistic in Statistics) {
            if (string.IsNullOrWhiteSpace(statistic.OnStatisticField)) {
                throw new InvalidOperationException("StatisticDefinition.OnStatisticField must be provided.");
            }

            if (string.IsNullOrWhiteSpace(statistic.OutStatisticFieldName)) {
                throw new InvalidOperationException("StatisticDefinition.OutStatisticFieldName must be provided.");
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
            }
            else if (statistic.PercentileParameters is not null) {
                throw new InvalidOperationException(
                    "PercentileParameters can only be used with percentile statistic types.");
            }
        }
    }

    private static void ValidateJsonObject(string? json, string propertyName, bool required) {
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
}