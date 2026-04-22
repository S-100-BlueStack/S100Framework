namespace S100Framework.REST.Models;

/// <summary>
/// Defines a top-features query for a layer.
/// </summary>
public sealed record TopFeaturesQuery
{
    /// <summary>
    /// Gets the SQL where clause used to filter candidate records.
    /// </summary>
    /// <remarks>
    /// Defaults to <c>1=1</c>, which matches all records.
    /// </remarks>
    public string Where { get; init; } = "1=1";

    /// <summary>
    /// Gets optional object IDs used to restrict the candidate records.
    /// </summary>
    public IReadOnlyList<long>? ObjectIds { get; init; }

    /// <summary>
    /// Gets the fields to return.
    /// </summary>
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
    /// Gets the output spatial reference ID for returned geometries.
    /// </summary>
    public int? OutSrid { get; init; }

    /// <summary>
    /// Gets the geometry precision to request from the service.
    /// </summary>
    public int? GeometryPrecision { get; init; }

    /// <summary>
    /// Gets the maximum allowable offset used to generalize returned geometry.
    /// </summary>
    public double? MaxAllowableOffset { get; init; }

    /// <summary>
    /// Gets the optional spatial filter applied to candidate records.
    /// </summary>
    public FeatureSpatialFilter? SpatialFilter { get; init; }

    /// <summary>
    /// Gets the top-features configuration.
    /// </summary>
    /// <remarks>
    /// This property is required.
    /// </remarks>
    public TopFeaturesFilter? TopFilter { get; init; }

    /// <summary>
    /// Validates the query configuration.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <see cref="TopFilter" /> is missing or when <see cref="ObjectIds" />
    /// is provided as an empty collection.
    /// </exception>
    public void Validate() {
        TopFilter?.Validate();

        if (TopFilter is null) {
            throw new InvalidOperationException("TopFilter must be provided.");
        }

        if (ObjectIds is { Count: 0 }) {
            throw new InvalidOperationException("ObjectIds must not be empty when provided.");
        }
    }
}