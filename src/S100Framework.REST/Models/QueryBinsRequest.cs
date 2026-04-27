using System.Text.Json;

namespace S100Framework.REST.Models;

/// <summary>
/// Represents a layer-level <c>queryBins</c> request.
/// </summary>
public sealed record QueryBinsRequest
{
    /// <summary>
    /// Gets the SQL WHERE clause used to filter records before binning.
    /// </summary>
    /// <remarks>
    /// Defaults to <c>1=1</c>, which matches all records.
    /// </remarks>
    public string Where { get; init; } = "1=1";

    /// <summary>
    /// Gets the raw JSON object sent through the required <c>bin</c> parameter.
    /// </summary>
    /// <remarks>
    /// Raw JSON is used intentionally because ArcGIS supports several bin shapes, including fixed interval,
    /// auto interval, fixed boundaries, date bins, classification bins, split bins, and stacked bins.
    /// </remarks>
    public string BinJson { get; init; } = string.Empty;

    /// <summary>
    /// Gets optional statistics calculated for each bin.
    /// </summary>
    /// <remarks>
    /// When omitted, ArcGIS returns the default histogram frequency for the configured bin.
    /// </remarks>
    public IReadOnlyList<StatisticDefinition>? Statistics { get; init; }

    /// <summary>
    /// Gets the optional bin ordering.
    /// </summary>
    public QueryBinsOrder? BinOrder { get; init; }

    /// <summary>
    /// Gets the optional raw JSON object used for the <c>outTimeReference</c> parameter.
    /// </summary>
    public string? OutTimeReferenceJson { get; init; }

    /// <summary>
    /// Gets the default spatial reference used for spatial inputs and outputs when specific references are omitted.
    /// </summary>
    public int? DefaultSrid { get; init; }

    /// <summary>
    /// Gets the output spatial reference used by geometry-producing bin statistics.
    /// </summary>
    public int? OutSrid { get; init; }

    /// <summary>
    /// Gets the well-known ID of the datum transformation applied to projected output geometries.
    /// </summary>
    /// <remarks>
    /// Use either this property or <see cref="DatumTransformationJson"/>, not both.
    /// </remarks>
    public int? DatumTransformationWkid { get; init; }

    /// <summary>
    /// Gets the raw datum transformation JSON object applied to projected output geometries.
    /// </summary>
    /// <remarks>
    /// Use either this property or <see cref="DatumTransformationWkid"/>, not both.
    /// </remarks>
    public string? DatumTransformationJson { get; init; }

    /// <summary>
    /// Gets the raw quantization parameters JSON object used for returned geometries.
    /// </summary>
    public string? QuantizationParametersJson { get; init; }

    /// <summary>
    /// Gets the optional alias used for the lower bin boundary attribute.
    /// </summary>
    public string? LowerBoundaryAlias { get; init; }

    /// <summary>
    /// Gets the optional alias used for the upper bin boundary attribute.
    /// </summary>
    public string? UpperBoundaryAlias { get; init; }

    /// <summary>
    /// Gets the optional spatial filter applied before binning.
    /// </summary>
    public FeatureSpatialFilter? SpatialFilter { get; init; }

    /// <summary>
    /// Validates the bin query request before it is sent.
    /// </summary>
    public void Validate() {
        ValidateJsonObject(BinJson, nameof(BinJson), required: true);
        ValidateJsonObject(OutTimeReferenceJson, nameof(OutTimeReferenceJson), required: false);
        ValidateJsonObject(DatumTransformationJson, nameof(DatumTransformationJson), required: false);
        ValidateJsonObject(QuantizationParametersJson, nameof(QuantizationParametersJson), required: false);

        if (Where is not null && string.IsNullOrWhiteSpace(Where)) {
            throw new InvalidOperationException("Where must not be empty when provided.");
        }

        if (DefaultSrid is <= 0) {
            throw new InvalidOperationException("DefaultSrid must be greater than zero when provided.");
        }

        if (OutSrid is <= 0) {
            throw new InvalidOperationException("OutSrid must be greater than zero when provided.");
        }

        if (DatumTransformationWkid is <= 0) {
            throw new InvalidOperationException("DatumTransformationWkid must be greater than zero when provided.");
        }

        if (DatumTransformationWkid.HasValue && DatumTransformationJson is not null) {
            throw new InvalidOperationException(
                "DatumTransformationWkid and DatumTransformationJson cannot both be specified.");
        }

        if (LowerBoundaryAlias is not null && string.IsNullOrWhiteSpace(LowerBoundaryAlias)) {
            throw new InvalidOperationException("LowerBoundaryAlias must not be empty when provided.");
        }

        if (UpperBoundaryAlias is not null && string.IsNullOrWhiteSpace(UpperBoundaryAlias)) {
            throw new InvalidOperationException("UpperBoundaryAlias must not be empty when provided.");
        }

        ValidateStatistics();
    }

    private void ValidateStatistics() {
        if (Statistics is null) {
            return;
        }

        if (Statistics.Count == 0) {
            throw new InvalidOperationException("Statistics must not be empty when provided.");
        }

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