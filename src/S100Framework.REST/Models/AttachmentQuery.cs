namespace S100Framework.REST.Models;

public sealed record AttachmentQuery
{
    public IReadOnlyList<long>? ObjectIds { get; init; }

    public string? DefinitionExpression { get; init; }

    public IReadOnlyList<string>? AttachmentTypes { get; init; }

    public IReadOnlyList<string>? Keywords { get; init; }

    public long? MinimumSizeBytes { get; init; }

    public long? MaximumSizeBytes { get; init; }

    public bool ReturnUrl { get; init; }

    public bool ReturnMetadata { get; init; }

    public void Validate() {
        var hasObjectIds = ObjectIds is { Count: > 0 };
        var hasDefinitionExpression = !string.IsNullOrWhiteSpace(DefinitionExpression);

        if (!hasObjectIds && !hasDefinitionExpression) {
            throw new InvalidOperationException(
                "At least one of ObjectIds or DefinitionExpression must be provided.");
        }

        if (ObjectIds is { Count: 0 }) {
            throw new InvalidOperationException("ObjectIds must not be empty when provided.");
        }

        if (MinimumSizeBytes is < 0) {
            throw new InvalidOperationException("MinimumSizeBytes must be greater than or equal to zero.");
        }

        if (MaximumSizeBytes is < 0) {
            throw new InvalidOperationException("MaximumSizeBytes must be greater than or equal to zero.");
        }

        if (MinimumSizeBytes.HasValue &&
            MaximumSizeBytes.HasValue &&
            MinimumSizeBytes.Value > MaximumSizeBytes.Value) {
            throw new InvalidOperationException(
                "MinimumSizeBytes must be less than or equal to MaximumSizeBytes.");
        }
    }
}