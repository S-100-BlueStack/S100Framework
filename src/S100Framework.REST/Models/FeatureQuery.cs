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
    /// <remarks>
    /// When this value is <see langword="true"/>, the service returns distinct values based on
    /// <see cref="OutFields"/>. Use <see cref="ReturnGeometry"/> set to <see langword="false"/> for reliable
    /// distinct-value results.
    /// </remarks>
    public bool ReturnDistinctValues { get; init; }

    /// <summary>
    /// Gets a value indicating whether the service should return the geometry centroid for each returned feature.
    /// </summary>
    /// <remarks>
    /// This parameter is only sent for feature queries. ArcGIS ignores centroid output for count and object ID queries.
    /// </remarks>
    public bool? ReturnCentroid { get; init; }

    /// <summary>
    /// Gets the geometry precision to request from the service.
    /// </summary>
    public int? GeometryPrecision { get; init; }

    /// <summary>
    /// Gets the maximum allowable offset used to generalize returned geometry.
    /// </summary>
    public double? MaxAllowableOffset { get; init; }

    /// <summary>
    /// Gets the raw quantization parameters JSON object used to project returned geometries onto a virtual grid.
    /// </summary>
    /// <remarks>
    /// This value is sent directly as the REST API's <c>quantizationParameters</c> parameter.
    /// </remarks>
    public string? QuantizationParametersJson { get; init; }

    /// <summary>
    /// Gets the multipatch geometry return mode.
    /// </summary>
    public FeatureQueryMultipatchOption? MultipatchOption { get; init; }

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
    /// Gets the result type used by the service when applying standard or tile max-record-count behavior.
    /// </summary>
    public FeatureQueryResultType? ResultType { get; init; }

    /// <summary>
    /// Gets a value indicating whether features should be returned when the response exceeds the transfer limit.
    /// </summary>
    /// <remarks>
    /// This is mainly useful with <see cref="FeatureQueryResultType.Tile"/> when a caller wants to detect whether a tile resolution still exceeds the transfer limit.
    /// </remarks>
    public bool? ReturnExceededLimitFeatures { get; init; }

    /// <summary>
    /// Gets a value indicating whether the service should use query response caching when supported.
    /// </summary>
    /// <remarks>
    /// This value is sent as the ArcGIS REST <c>cacheHint</c> parameter. Consumers can inspect
    /// <see cref="FeatureLayerCapabilities.SupportsQueryWithCacheHint"/> before enabling it.
    /// </remarks>
    public bool? CacheHint { get; init; }

    /// <summary>
    /// Gets the default spatial reference ID applied to spatial query parameters when supported by the service.
    /// </summary>
    public int? DefaultSrid { get; init; }

    /// <summary>
    /// Gets the output spatial reference ID for returned geometries.
    /// </summary>
    public int? OutSrid { get; init; }

    /// <summary>
    /// Gets the well-known ID of the datum transformation applied to projected input geometries.
    /// </summary>
    /// <remarks>
    /// Use either this property or <see cref="DatumTransformationJson"/>, not both.
    /// </remarks>
    public int? DatumTransformationWkid { get; init; }

    /// <summary>
    /// Gets the raw datum transformation JSON object applied to projected input geometries.
    /// </summary>
    /// <remarks>
    /// Use this for WKT-based or composite datum transformations. Use either this property or <see cref="DatumTransformationWkid"/>, not both.
    /// </remarks>
    public string? DatumTransformationJson { get; init; }

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