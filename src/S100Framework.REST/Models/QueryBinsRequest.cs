using S100Framework.REST.Internal.Validation;

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
        QueryBinRequestValidation.ValidateJsonObject(BinJson, nameof(BinJson), required: true);
        QueryBinRequestValidation.ValidateJsonObject(OutTimeReferenceJson, nameof(OutTimeReferenceJson), required: false);
        QueryBinRequestValidation.ValidateJsonObject(DatumTransformationJson, nameof(DatumTransformationJson), required: false);
        QueryBinRequestValidation.ValidateJsonObject(QuantizationParametersJson, nameof(QuantizationParametersJson), required: false);

        if (Where is not null && string.IsNullOrWhiteSpace(Where)) {
            throw new InvalidOperationException("Where must not be empty when provided.");
        }

        QueryBinRequestValidation.ValidateBinOrder(BinOrder);

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

        QueryBinRequestValidation.ValidateStatistics(Statistics, required: false);
    }
}