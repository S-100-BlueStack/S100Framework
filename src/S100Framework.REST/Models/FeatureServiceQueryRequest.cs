namespace S100Framework.REST.Models;

/// <summary>
/// Represents a service-level <c>query</c> request against one or more feature service layers or tables.
/// </summary>
public sealed record FeatureServiceQueryRequest
{
    /// <summary>
    /// Gets the layer definitions included in the service-level query.
    /// </summary>
    /// <remarks>
    /// These definitions are serialized to the REST API's <c>layerDefs</c> parameter.
    /// </remarks>
    public IReadOnlyList<FeatureServiceLayerQueryDefinition> LayerDefinitions { get; init; } =
        Array.Empty<FeatureServiceLayerQueryDefinition>();

    /// <summary>
    /// Gets a value indicating whether returned feature records should include geometry.
    /// </summary>
    public bool ReturnGeometry { get; init; } = true;

    /// <summary>
    /// Gets a value indicating whether Z values should be returned when available.
    /// </summary>
    public bool? ReturnZ { get; init; }

    /// <summary>
    /// Gets a value indicating whether M values should be returned when available.
    /// </summary>
    public bool? ReturnM { get; init; }

    /// <summary>
    /// Gets the output spatial reference used for returned geometries.
    /// </summary>
    public int? OutSrid { get; init; }

    /// <summary>
    /// Gets the geometry precision requested from the service.
    /// </summary>
    public int? GeometryPrecision { get; init; }

    /// <summary>
    /// Gets the maximum allowable offset used to generalize returned geometries.
    /// </summary>
    public double? MaxAllowableOffset { get; init; }

    /// <summary>
    /// Gets the geodatabase version to query when supported by the service.
    /// </summary>
    public string? GdbVersion { get; init; }

    /// <summary>
    /// Gets the historic moment to query.
    /// </summary>
    public DateTimeOffset? HistoricMoment { get; init; }

    /// <summary>
    /// Gets the SQL format requested from the service.
    /// </summary>
    public FeatureQuerySqlFormat? SqlFormat { get; init; }

    /// <summary>
    /// Gets the optional spatial filter applied to all queried layers and tables.
    /// </summary>
    public FeatureSpatialFilter? SpatialFilter { get; init; }

    /// <summary>
    /// Gets the time instant to query.
    /// </summary>
    /// <remarks>
    /// Use <see cref="TimeExtent" /> for ranged or open-ended temporal queries.
    /// </remarks>
    public DateTimeOffset? TimeInstant { get; init; }

    /// <summary>
    /// Gets the time extent to query.
    /// </summary>
    /// <remarks>
    /// Use <see cref="TimeInstant" /> for a single instant.
    /// </remarks>
    public FeatureTimeExtent? TimeExtent { get; init; }

    /// <summary>
    /// Gets a value indicating whether the client can work with date fields whose time reference is unknown.
    /// </summary>
    public bool TimeReferenceUnknownClient { get; init; }

    /// <summary>
    /// Validates the service-level query request before it is sent.
    /// </summary>
    public void Validate() {
        if (LayerDefinitions is not { Count: > 0 }) {
            throw new InvalidOperationException("At least one layer definition must be provided.");
        }

        var layerIds = new HashSet<int>();

        foreach (var layerDefinition in LayerDefinitions) {
            if (layerDefinition is null) {
                throw new InvalidOperationException("LayerDefinitions must not contain null values.");
            }

            layerDefinition.Validate();

            if (!layerIds.Add(layerDefinition.LayerId)) {
                throw new InvalidOperationException(
                    $"Duplicate layer definition for layer ID {layerDefinition.LayerId} is not allowed.");
            }
        }

        if (TimeInstant.HasValue && TimeExtent is not null) {
            throw new InvalidOperationException("TimeInstant and TimeExtent cannot both be specified.");
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

        if (GeometryPrecision is < 0) {
            throw new InvalidOperationException("GeometryPrecision must be greater than or equal to zero when provided.");
        }

        if (MaxAllowableOffset is < 0) {
            throw new InvalidOperationException("MaxAllowableOffset must be greater than or equal to zero when provided.");
        }

        if (GdbVersion is not null && string.IsNullOrWhiteSpace(GdbVersion)) {
            throw new InvalidOperationException("GdbVersion must not be empty when provided.");
        }

        if (SqlFormat.HasValue && !Enum.IsDefined(SqlFormat.Value)) {
            throw new InvalidOperationException("SqlFormat must be a supported SQL format.");
        }
    }
}