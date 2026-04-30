using System.Text.Json;

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
    /// Gets the zero-based related-record result offset to request from the service.
    /// </summary>
    /// <remarks>
    /// This parameter requires the layer to advertise related-record query pagination support.
    /// </remarks>
    public int? ResultOffset { get; init; }

    /// <summary>
    /// Gets the maximum number of related records to return.
    /// </summary>
    /// <remarks>
    /// This parameter requires the layer to advertise related-record query pagination support.
    /// </remarks>
    public int? ResultRecordCount { get; init; }

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
    /// Gets the geodatabase version to query.
    /// </summary>
    public string? GdbVersion { get; init; }

    /// <summary>
    /// Gets the historic moment to query.
    /// </summary>
    public DateTimeOffset? HistoricMoment { get; init; }

    /// <summary>
    /// Gets a value indicating whether the client can work with date values in an unknown time zone.
    /// </summary>
    public bool TimeReferenceUnknownClient { get; init; }

    /// <summary>
    /// Gets the geometry precision to request from the service.
    /// </summary>
    public int? GeometryPrecision { get; init; }

    /// <summary>
    /// Gets the maximum allowable offset used to generalize returned geometry.
    /// </summary>
    public double? MaxAllowableOffset { get; init; }

    /// <summary>
    /// Gets the well-known ID of the datum transformation to apply when projecting related record geometries.
    /// </summary>
    /// <remarks>
    /// Use either this property or <see cref="DatumTransformationJson"/>, not both.
    /// </remarks>
    public int? DatumTransformationWkid { get; init; }

    /// <summary>
    /// Gets a JSON datum transformation definition to apply when projecting related record geometries.
    /// </summary>
    /// <remarks>
    /// Use this for WKT-based or composite datum transformations. Use either this property or
    /// <see cref="DatumTransformationWkid"/>, not both.
    /// </remarks>
    public string? DatumTransformationJson { get; init; }

    /// <summary>
    /// Gets the order-by clause for returned related records.
    /// </summary>
    /// <remarks>
    /// This parameter requires the layer to advertise advanced related-record query support.
    /// </remarks>
    public string? OrderBy { get; init; }

    /// <summary>
    /// Validates the query configuration.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the query configuration is invalid.
    /// </exception>
    public void Validate() {
        if (ObjectIds.Count == 0) {
            throw new InvalidOperationException("At least one source object ID must be provided.");
        }

        if (RelationshipId < 0) {
            throw new InvalidOperationException("RelationshipId must be greater than or equal to zero.");
        }

        if (ResultOffset is < 0) {
            throw new InvalidOperationException("ResultOffset must be greater than or equal to zero when provided.");
        }

        if (ResultRecordCount is <= 0) {
            throw new InvalidOperationException("ResultRecordCount must be greater than zero when provided.");
        }

        if (GeometryPrecision is < 0) {
            throw new InvalidOperationException("GeometryPrecision must be greater than or equal to zero when provided.");
        }

        if (MaxAllowableOffset is < 0) {
            throw new InvalidOperationException("MaxAllowableOffset must be greater than or equal to zero when provided.");
        }

        if (DatumTransformationWkid is <= 0) {
            throw new InvalidOperationException("DatumTransformationWkid must be greater than zero when provided.");
        }

        if (DatumTransformationWkid.HasValue && DatumTransformationJson is not null) {
            throw new InvalidOperationException(
                "DatumTransformationWkid and DatumTransformationJson cannot both be specified.");
        }

        if (OrderBy is not null && string.IsNullOrWhiteSpace(OrderBy)) {
            throw new InvalidOperationException("OrderBy must not be empty when provided.");
        }

        ValidateJsonObject(DatumTransformationJson, nameof(DatumTransformationJson));
    }

    private static void ValidateJsonObject(string? json, string propertyName) {
        if (json is null) {
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