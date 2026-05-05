using NetTopologySuite.Geometries;

namespace S100Framework.REST.Models;

/// <summary>
/// Represents a layer-level <c>applyEdits</c> request.
/// </summary>
/// <remarks>
/// Use this model for edit operations that target a single layer through
/// <c>IFeatureLayerClient</c>.
/// </remarks>
public sealed record FeatureEdits
{
    /// <summary>
    /// Gets the features to add.
    /// </summary>
    /// <remarks>
    /// When provided, the collection must contain at least one item.
    /// </remarks>
    public IReadOnlyList<EditableFeature>? Adds { get; init; }

    /// <summary>
    /// Gets the features to update.
    /// </summary>
    /// <remarks>
    /// When provided, the collection must contain at least one item.
    /// </remarks>
    public IReadOnlyList<EditableFeature>? Updates { get; init; }

    /// <summary>
    /// Gets the object IDs to delete.
    /// </summary>
    /// <remarks>
    /// When provided, the collection must contain at least one item.
    /// </remarks>
    public IReadOnlyList<long>? Deletes { get; init; }

    /// <summary>
    /// Gets a value indicating whether the server should roll back the full request
    /// if one edit operation fails.
    /// </summary>
    /// <remarks>
    /// The default is <see langword="true"/>.
    /// </remarks>
    public bool RollbackOnFailure { get; init; } = true;

    /// <summary>
    /// Gets a value indicating whether the request should use global IDs where supported.
    /// </summary>
    /// <remarks>
    /// For layer-level edits, this primarily affects how the request is serialized for
    /// services that support global-ID-based edit flows.
    /// </remarks>
    public bool UseGlobalIds { get; init; }

    /// <summary>
    /// Validates the edit request before it is sent to the server.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the request does not contain any edits, or when one of the provided
    /// edit collections is empty or contains invalid values.
    /// </exception>
    public void Validate() {
        var hasAdds = Adds is { Count: > 0 };
        var hasUpdates = Updates is { Count: > 0 };
        var hasDeletes = Deletes is { Count: > 0 };

        if (!hasAdds && !hasUpdates && !hasDeletes) {
            throw new InvalidOperationException(
                "At least one of Adds, Updates, or Deletes must be provided.");
        }

        ValidateEditableFeatures(Adds, nameof(Adds));
        ValidateEditableFeatures(Updates, nameof(Updates));
        ValidateDeleteObjectIds(Deletes, nameof(Deletes));
    }

    private static void ValidateEditableFeatures(
    IReadOnlyList<EditableFeature>? features,
    string propertyName) {
        if (features is null) {
            return;
        }

        if (features.Count == 0) {
            throw new InvalidOperationException($"{propertyName} must not be empty when provided.");
        }

        foreach (var feature in features) {
            if (feature is null) {
                throw new InvalidOperationException($"{propertyName} must not contain null values.");
            }

            if (feature.Attributes is null) {
                throw new InvalidOperationException($"{propertyName}.Attributes must be provided.");
            }

            if (feature.Attributes.Keys.Any(static key => string.IsNullOrWhiteSpace(key))) {
                throw new InvalidOperationException($"{propertyName}.Attributes must not contain empty attribute names.");
            }
        }
    }

    private static void ValidateDeleteObjectIds(
        IReadOnlyList<long>? objectIds,
        string propertyName) {
        if (objectIds is null) {
            return;
        }

        if (objectIds.Count == 0) {
            throw new InvalidOperationException($"{propertyName} must not be empty when provided.");
        }

        if (objectIds.Any(static objectId => objectId <= 0)) {
            throw new InvalidOperationException($"{propertyName} must contain only positive values.");
        }

        if (objectIds.Distinct().Count() != objectIds.Count) {
            throw new InvalidOperationException($"{propertyName} must not contain duplicate values.");
        }
    }
}

/// <summary>
/// Represents a single feature payload for add or update operations.
/// </summary>
/// <param name="Geometry">
/// The feature geometry to send to the server.
/// </param>
/// <param name="Attributes">
/// The feature attributes to send to the server.
/// </param>
/// <remarks>
/// For update operations, the attributes typically include the server object ID or
/// another identifier required by the target service.
/// </remarks>
public sealed record EditableFeature(
    Geometry? Geometry,
    IReadOnlyDictionary<string, object?> Attributes);