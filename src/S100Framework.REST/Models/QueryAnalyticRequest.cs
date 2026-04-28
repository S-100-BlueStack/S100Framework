using System.Text.Json;

namespace S100Framework.REST.Models;

/// <summary>
/// Represents a layer-level <c>queryAnalytic</c> request.
/// </summary>
public sealed record QueryAnalyticRequest
{
    /// <summary>
    /// Gets the SQL WHERE clause used to filter source rows before analytics are computed.
    /// </summary>
    /// <remarks>
    /// Defaults to <c>1=1</c>, which matches all records.
    /// </remarks>
    public string Where { get; init; } = "1=1";

    /// <summary>
    /// Gets the optional SQL WHERE clause applied after the analytic result set has been computed.
    /// </summary>
    public string? AnalyticWhere { get; init; }

    /// <summary>
    /// Gets the raw JSON array sent through the required <c>outAnalytics</c> parameter.
    /// </summary>
    /// <remarks>
    /// Raw JSON is used intentionally because ArcGIS supports many analytic definitions, including aggregate,
    /// ranking, window, expression, percentile, and linear regression analytics.
    /// </remarks>
    public string OutAnalyticsJson { get; init; } = string.Empty;

    /// <summary>
    /// Gets the fields included in the returned rows.
    /// </summary>
    /// <remarks>
    /// When omitted or empty, <c>*</c> is sent to the service.
    /// </remarks>
    public IReadOnlyList<string>? OutFields { get; init; }

    /// <summary>
    /// Gets a value indicating whether geometry should be returned with each result row.
    /// </summary>
    public bool ReturnGeometry { get; init; } = true;

    /// <summary>
    /// Gets the output spatial reference used for returned geometries.
    /// </summary>
    public int? OutSrid { get; init; }

    /// <summary>
    /// Gets the order-by clause for the analytic query.
    /// </summary>
    public string? OrderBy { get; init; }

    /// <summary>
    /// Gets the result type used by the service when applying standard or tile max-record-count behavior.
    /// </summary>
    public FeatureQueryResultType? ResultType { get; init; }

    /// <summary>
    /// Gets a value indicating whether the service may cache the analytic query result.
    /// </summary>
    public bool? CacheHint { get; init; }

    /// <summary>
    /// Gets the result offset used for paged analytic queries.
    /// </summary>
    public int? ResultOffset { get; init; }

    /// <summary>
    /// Gets the maximum number of analytic rows to return.
    /// </summary>
    public int? ResultRecordCount { get; init; }

    /// <summary>
    /// Gets the raw quantization parameters JSON object used for returned geometries.
    /// </summary>
    public string? QuantizationParametersJson { get; init; }

    /// <summary>
    /// Gets the SQL format requested from the service.
    /// </summary>
    public FeatureQuerySqlFormat? SqlFormat { get; init; }

    /// <summary>
    /// Gets the optional spatial filter applied before analytics are computed.
    /// </summary>
    public FeatureSpatialFilter? SpatialFilter { get; init; }

    /// <summary>
    /// Validates the analytic query request before it is sent.
    /// </summary>
    public void Validate() {
        ValidateJsonArray(OutAnalyticsJson, nameof(OutAnalyticsJson), required: true);
        ValidateJsonObject(QuantizationParametersJson, nameof(QuantizationParametersJson), required: false);

        if (Where is not null && string.IsNullOrWhiteSpace(Where)) {
            throw new InvalidOperationException("Where must not be empty when provided.");
        }

        if (AnalyticWhere is not null && string.IsNullOrWhiteSpace(AnalyticWhere)) {
            throw new InvalidOperationException("AnalyticWhere must not be empty when provided.");
        }

        if (OutFields?.Any(static field => string.IsNullOrWhiteSpace(field)) == true) {
            throw new InvalidOperationException("OutFields must not contain null, empty, or whitespace-only values.");
        }

        if (OutSrid is <= 0) {
            throw new InvalidOperationException("OutSrid must be greater than zero when provided.");
        }

        if (OrderBy is not null && string.IsNullOrWhiteSpace(OrderBy)) {
            throw new InvalidOperationException("OrderBy must not be empty when provided.");
        }

        if (ResultOffset is < 0) {
            throw new InvalidOperationException("ResultOffset must not be negative when provided.");
        }

        if (ResultRecordCount is <= 0) {
            throw new InvalidOperationException("ResultRecordCount must be greater than zero when provided.");
        }
    }

    private static void ValidateJsonArray(
        string? json,
        string propertyName,
        bool required) {
        ValidateJson(json, propertyName, required, JsonValueKind.Array);
    }

    private static void ValidateJsonObject(
        string? json,
        string propertyName,
        bool required) {
        ValidateJson(json, propertyName, required, JsonValueKind.Object);
    }

    private static void ValidateJson(
        string? json,
        string propertyName,
        bool required,
        JsonValueKind expectedKind) {
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

            if (document.RootElement.ValueKind != expectedKind) {
                throw new InvalidOperationException($"{propertyName} must be a JSON {expectedKind.ToString().ToLowerInvariant()}.");
            }
        }
        catch (JsonException exception) {
            throw new InvalidOperationException($"{propertyName} must contain valid JSON.", exception);
        }
    }
}