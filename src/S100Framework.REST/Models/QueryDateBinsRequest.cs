using S100Framework.REST.Internal.Validation;

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

        QueryBinRequestValidation.ValidateJsonObject(BinJson, nameof(BinJson), required: true);
        QueryBinRequestValidation.ValidateJsonObject(QuantizationParametersJson, nameof(QuantizationParametersJson), required: false);

        QueryBinRequestValidation.ValidateStatistics(Statistics, required: true);

        if (Where is not null && string.IsNullOrWhiteSpace(Where)) {
            throw new InvalidOperationException("Where must not be empty when provided.");
        }

        QueryBinRequestValidation.ValidateBinOrder(BinOrder);

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
    }
}