namespace S100Framework.REST.Models;

/// <summary>
/// Represents a service-level <c>applyEdits</c> request across one or more layers.
/// </summary>
/// <remarks>
/// Use this model for multi-layer edit operations through <c>IFeatureServiceClient</c>.
/// </remarks>
public sealed record FeatureServiceEdits
{
    /// <summary>
    /// Gets the layer edit blocks included in the request.
    /// </summary>
    /// <remarks>
    /// The collection must contain at least one layer edit block.
    /// </remarks>
    public IReadOnlyList<ServiceLayerEdits> Layers { get; init; } = Array.Empty<ServiceLayerEdits>();

    /// <summary>
    /// Gets a value indicating whether the server should roll back the full request
    /// if one layer edit operation fails.
    /// </summary>
    /// <remarks>
    /// The default is <see langword="true" />.
    /// </remarks>
    public bool RollbackOnFailure { get; init; } = true;

    /// <summary>
    /// Gets a value indicating whether deletes should use global IDs instead of object IDs.
    /// </summary>
    /// <remarks>
    /// When enabled, each <see cref="ServiceLayerEdits"/> block must use
    /// <see cref="ServiceLayerEdits.DeleteGlobalIds"/> instead of
    /// <see cref="ServiceLayerEdits.DeleteObjectIds"/>.
    /// </remarks>
    public bool UseGlobalIds { get; init; }

    /// <summary>
    /// Validates the service-level edit request before it is sent to the server.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the request does not contain any layer edits, or when one of the
    /// contained layer edit blocks is invalid.
    /// </exception>
    public void Validate() {
        if (Layers is not { Count: > 0 }) {
            throw new InvalidOperationException("At least one layer edit block must be provided.");
        }

        var layerIds = new HashSet<int>();

        foreach (var layer in Layers) {
            if (layer is null) {
                throw new InvalidOperationException("Layers must not contain null values.");
            }

            layer.Validate(UseGlobalIds);

            if (!layerIds.Add(layer.LayerId)) {
                throw new InvalidOperationException(
                    $"Duplicate layer edit block for layer ID {layer.LayerId} is not allowed.");
            }
        }
    }
}

/// <summary>
/// Represents the edit operations for a single layer within a service-level
/// <c>applyEdits</c> request.
/// </summary>
public sealed record ServiceLayerEdits
{
    /// <summary>
    /// Gets the target layer ID.
    /// </summary>
    public required int LayerId { get; init; }

    /// <summary>
    /// Gets the features to add to the layer.
    /// </summary>
    /// <remarks>
    /// When provided, the collection must contain at least one item.
    /// </remarks>
    public IReadOnlyList<EditableFeature>? Adds { get; init; }

    /// <summary>
    /// Gets the features to update in the layer.
    /// </summary>
    /// <remarks>
    /// When provided, the collection must contain at least one item.
    /// </remarks>
    public IReadOnlyList<EditableFeature>? Updates { get; init; }

    /// <summary>
    /// Gets the object IDs to delete from the layer.
    /// </summary>
    /// <remarks>
    /// Use this only when service-level <see cref="FeatureServiceEdits.UseGlobalIds"/>
    /// is disabled.
    /// </remarks>
    public IReadOnlyList<long>? DeleteObjectIds { get; init; }

    /// <summary>
    /// Gets the global IDs to delete from the layer.
    /// </summary>
    /// <remarks>
    /// Use this only when service-level <see cref="FeatureServiceEdits.UseGlobalIds"/>
    /// is enabled.
    /// </remarks>
    public IReadOnlyList<string>? DeleteGlobalIds { get; init; }

    /// <summary>
    /// Validates the layer edit block before it is sent to the server.
    /// </summary>
    /// <param name="useGlobalIds">
    /// Indicates whether the parent service-level request uses global IDs for deletes.
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the layer edit block is inconsistent or incomplete.
    /// </exception>
    public void Validate(bool useGlobalIds) {
        var hasAdds = Adds is { Count: > 0 };
        var hasUpdates = Updates is { Count: > 0 };
        var hasDeleteObjectIds = DeleteObjectIds is { Count: > 0 };
        var hasDeleteGlobalIds = DeleteGlobalIds is { Count: > 0 };

        if (LayerId < 0) {
            throw new InvalidOperationException("LayerId must be greater than or equal to zero.");
        }

        if (!hasAdds && !hasUpdates && !hasDeleteObjectIds && !hasDeleteGlobalIds) {
            throw new InvalidOperationException(
                $"Layer {LayerId} must contain at least one add, update, or delete operation.");
        }

        ValidateEditableFeatures(Adds, nameof(Adds));
        ValidateEditableFeatures(Updates, nameof(Updates));
        ValidateDeleteObjectIds(DeleteObjectIds, nameof(DeleteObjectIds));
        ValidateDeleteGlobalIds(DeleteGlobalIds, nameof(DeleteGlobalIds));

        if (hasDeleteObjectIds && hasDeleteGlobalIds) {
            throw new InvalidOperationException(
                "DeleteObjectIds and DeleteGlobalIds cannot both be provided for the same layer.");
        }

        if (useGlobalIds && hasDeleteObjectIds) {
            throw new InvalidOperationException(
                "DeleteObjectIds cannot be used when UseGlobalIds is enabled. Use DeleteGlobalIds instead.");
        }

        if (!useGlobalIds && hasDeleteGlobalIds) {
            throw new InvalidOperationException(
                "DeleteGlobalIds cannot be used when UseGlobalIds is disabled. Use DeleteObjectIds instead.");
        }
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

    private static void ValidateDeleteGlobalIds(
        IReadOnlyList<string>? globalIds,
        string propertyName) {
        if (globalIds is null) {
            return;
        }

        if (globalIds.Count == 0) {
            throw new InvalidOperationException($"{propertyName} must not be empty when provided.");
        }

        if (globalIds.Any(string.IsNullOrWhiteSpace)) {
            throw new InvalidOperationException($"{propertyName} must not contain empty values.");
        }

        if (globalIds.Distinct(StringComparer.OrdinalIgnoreCase).Count() != globalIds.Count) {
            throw new InvalidOperationException($"{propertyName} must not contain duplicate values.");
        }
    }
}