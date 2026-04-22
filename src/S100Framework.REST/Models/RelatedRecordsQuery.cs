namespace S100Framework.REST.Models;

/// <summary>
/// Defines a related-records query for a relationship on the current layer.
/// </summary>
public sealed record RelatedRecordsQuery
{
    /// <summary>
    /// Gets the source feature object IDs.
    /// </summary>
    /// <remarks>
    /// At least one source object ID must be provided.
    /// </remarks>
    public IReadOnlyList<long> ObjectIds { get; init; } = Array.Empty<long>();

    /// <summary>
    /// Gets the relationship ID to query through.
    /// </summary>
    public required int RelationshipId { get; init; }

    /// <summary>
    /// Gets the fields to return from related records.
    /// </summary>
    public IReadOnlyList<string>? OutFields { get; init; }

    /// <summary>
    /// Gets an optional definition expression applied to related records.
    /// </summary>
    public string? DefinitionExpression { get; init; }

    /// <summary>
    /// Gets a value indicating whether related record geometry should be returned.
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
    /// Gets the order-by clause for returned related records.
    /// </summary>
    public string? OrderBy { get; init; }

    /// <summary>
    /// Validates the query configuration.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no source object IDs are provided or when <see cref="RelationshipId" />
    /// is negative.
    /// </exception>
    public void Validate() {
        if (ObjectIds.Count == 0) {
            throw new InvalidOperationException("At least one source object ID must be provided.");
        }

        if (RelationshipId < 0) {
            throw new InvalidOperationException("RelationshipId must be greater than or equal to zero.");
        }
    }
}