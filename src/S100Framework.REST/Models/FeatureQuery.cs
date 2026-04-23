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
    /// Gets the output spatial reference ID for returned geometries.
    /// </summary>
    public int? OutSrid { get; init; }

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