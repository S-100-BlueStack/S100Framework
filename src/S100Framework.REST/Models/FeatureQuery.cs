namespace S100Framework.REST.Models;

/// <summary>
/// Defines a standard feature query for a layer or table.
/// </summary>
public sealed record FeatureQuery
{
    /// <summary>
    /// Gets the SQL where clause used to filter records.
    /// </summary>
    /// <remarks>
    /// Defaults to <c>1=1</c>, which matches all records.
    /// </remarks>
    public string Where { get; init; } = "1=1";

    /// <summary>
    /// Gets the fields to return.
    /// </summary>
    /// <remarks>
    /// When <see langword="null"/>, the service default behavior is used.
    /// </remarks>
    public IReadOnlyList<string>? OutFields { get; init; }

    /// <summary>
    /// Gets a value indicating whether geometry should be returned.
    /// </summary>
    public bool ReturnGeometry { get; init; } = true;

    /// <summary>
    /// Gets a value indicating whether Z values should be returned when supported.
    /// </summary>
    public bool? ReturnZ { get; init; }

    /// <summary>
    /// Gets a value indicating whether M values should be returned when supported.
    /// </summary>
    public bool? ReturnM { get; init; }

    /// <summary>
    /// Gets a value indicating whether distinct values should be returned.
    /// </summary>
    public bool ReturnDistinctValues { get; init; }

    /// <summary>
    /// Gets the geometry precision to request from the service.
    /// </summary>
    public int? GeometryPrecision { get; init; }

    /// <summary>
    /// Gets the maximum allowable offset used to generalize returned geometry.
    /// </summary>
    public double? MaxAllowableOffset { get; init; }

    /// <summary>
    /// Gets the requested page size.
    /// </summary>
    /// <remarks>
    /// This controls the per-request batch size used by the client when it pages through results.
    /// </remarks>
    public int? PageSize { get; init; }

    /// <summary>
    /// Gets the order-by clause for the query.
    /// </summary>
    public string? OrderBy { get; init; }

    /// <summary>
    /// Gets the maximum number of records to return.
    /// </summary>
    public int? Limit { get; init; }

    /// <summary>
    /// Gets the number of records to skip before returning results from paged feature queries.
    /// </summary>
    /// <remarks>
    /// This is applied as the starting offset for <c>QueryAsync</c> when the layer supports pagination.
    /// </remarks>
    public int? ResultOffset { get; init; }

    /// <summary>
    /// Gets the maximum number of records to return after applying <see cref="ResultOffset"/> for paged feature queries.
    /// </summary>
    /// <remarks>
    /// This defines the overall requested result window. It does not replace <see cref="PageSize"/>, which still controls batch size.
    /// </remarks>
    public int? ResultRecordCount { get; init; }

    /// <summary>
    /// Gets the default spatial reference ID applied to spatial query parameters when supported by the service.
    /// </summary>
    public int? DefaultSrid { get; init; }

    /// <summary>
    /// Gets the output spatial reference ID for returned geometries.
    /// </summary>
    public int? OutSrid { get; init; }

    /// <summary>
    /// Gets the SQL format requested from the service.
    /// </summary>
    public FeatureQuerySqlFormat? SqlFormat { get; init; }

    /// <summary>
    /// Gets a value indicating whether the service should return feature geometries as envelopes instead of full geometries.
    /// </summary>
    /// <remarks>
    /// This only applies to feature queries that return geometry and requires layer capability support.
    /// </remarks>
    public bool ReturnEnvelope { get; init; }

    /// <summary>
    /// Gets the full text search expressions to apply to the query.
    /// </summary>
    /// <remarks>
    /// When specified, these expressions are serialized to the REST API's <c>fullText</c> query parameter.
    /// </remarks>
    public IReadOnlyList<FeatureQueryFullTextExpression>? FullText { get; init; }

    /// <summary>
    /// Gets the unique IDs to query.
    /// </summary>
    /// <remarks>
    /// Simple unique IDs contain a single component. Composite unique IDs contain multiple ordered components.
    /// </remarks>
    public IReadOnlyList<FeatureUniqueId>? UniqueIds { get; init; }

    /// <summary>
    /// Gets the optional spatial filter applied to the query.
    /// </summary>
    public FeatureSpatialFilter? SpatialFilter { get; init; }

    /// <summary>
    /// Gets the time instant to query.
    /// </summary>
    /// <remarks>
    /// Use <see cref="TimeExtent"/> for ranged or open-ended temporal queries.
    /// </remarks>
    public DateTimeOffset? TimeInstant { get; init; }

    /// <summary>
    /// Gets the time extent to query.
    /// </summary>
    public FeatureTimeExtent? TimeExtent { get; init; }

    /// <summary>
    /// Gets the historic moment to query when the layer supports historic moment queries.
    /// </summary>
    public DateTimeOffset? HistoricMoment { get; init; }
}