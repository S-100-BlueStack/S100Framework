namespace S100Framework.REST.Models;

public sealed record FeatureServiceEdits
{
    public IReadOnlyList<ServiceLayerEdits> Layers { get; init; } = Array.Empty<ServiceLayerEdits>();

    public bool RollbackOnFailure { get; init; } = true;

    public bool UseGlobalIds { get; init; }

    public void Validate() {
        if (Layers.Count == 0) {
            throw new InvalidOperationException("At least one layer edit block must be provided.");
        }

        foreach (var layer in Layers) {
            layer.Validate(UseGlobalIds);
        }
    }
}

public sealed record ServiceLayerEdits
{
    public required int LayerId { get; init; }

    public IReadOnlyList<EditableFeature>? Adds { get; init; }

    public IReadOnlyList<EditableFeature>? Updates { get; init; }

    public IReadOnlyList<long>? DeleteObjectIds { get; init; }

    public IReadOnlyList<string>? DeleteGlobalIds { get; init; }

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