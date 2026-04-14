using NetTopologySuite.Geometries;

namespace S100Framework.REST.Models;

public sealed record FeatureEdits
{
    public IReadOnlyList<EditableFeature>? Adds { get; init; }

    public IReadOnlyList<EditableFeature>? Updates { get; init; }

    public IReadOnlyList<long>? Deletes { get; init; }

    public bool RollbackOnFailure { get; init; } = true;

    public bool UseGlobalIds { get; init; }

    public void Validate() {
        var hasAdds = Adds is { Count: > 0 };
        var hasUpdates = Updates is { Count: > 0 };
        var hasDeletes = Deletes is { Count: > 0 };

        if (!hasAdds && !hasUpdates && !hasDeletes) {
            throw new InvalidOperationException(
                "At least one of Adds, Updates, or Deletes must be provided.");
        }

        if (Adds is { Count: 0 }) {
            throw new InvalidOperationException("Adds must not be empty when provided.");
        }

        if (Updates is { Count: 0 }) {
            throw new InvalidOperationException("Updates must not be empty when provided.");
        }

        if (Deletes is { Count: 0 }) {
            throw new InvalidOperationException("Deletes must not be empty when provided.");
        }
    }
}

public sealed record EditableFeature(
    Geometry? Geometry,
    IReadOnlyDictionary<string, object?> Attributes);