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
        if (Layers.Count == 0) {
            throw new InvalidOperationException("At least one layer edit block must be provided.");
        }

        foreach (var layer in Layers) {
            layer.Validate(UseGlobalIds);
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

        if (!hasAdds && !hasUpdates && !hasDeleteObjectIds && !hasDeleteGlobalIds) {
            throw new InvalidOperationException(
                $"Layer {LayerId} must contain at least one add, update, or delete operation.");
        }

        if (LayerId < 0) {
            throw new InvalidOperationException("LayerId must be greater than or equal to zero.");
        }

        if (DeleteObjectIds is { Count: 0 }) {
            throw new InvalidOperationException("DeleteObjectIds must not be empty when provided.");
        }

        if (DeleteGlobalIds is { Count: 0 }) {
            throw new InvalidOperationException("DeleteGlobalIds must not be empty when provided.");
        }

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
}