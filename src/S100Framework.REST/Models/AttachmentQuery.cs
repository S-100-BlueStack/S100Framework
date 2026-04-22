namespace S100Framework.REST.Models;

/// <summary>
/// Defines a query for attachment metadata on a layer.
/// </summary>
public sealed record AttachmentQuery
{
    /// <summary>
    /// Gets the object IDs to match.
    /// </summary>
    /// <remarks>
    /// At least one of <see cref="ObjectIds"/> or <see cref="DefinitionExpression"/> must be provided.
    /// </remarks>
    public IReadOnlyList<long>? ObjectIds { get; init; }

    /// <summary>
    /// Gets the layer definition expression used to select parent features.
    /// </summary>
    /// <remarks>
    /// At least one of <see cref="ObjectIds"/> or <see cref="DefinitionExpression"/> must be provided.
    /// </remarks>
    public string? DefinitionExpression { get; init; }

    /// <summary>
    /// Gets optional attachment content types to include.
    /// </summary>
    public IReadOnlyList<string>? AttachmentTypes { get; init; }

    /// <summary>
    /// Gets optional attachment keywords to include.
    /// </summary>
    public IReadOnlyList<string>? Keywords { get; init; }

    /// <summary>
    /// Gets the minimum attachment size, in bytes.
    /// </summary>
    public long? MinimumSizeBytes { get; init; }

    /// <summary>
    /// Gets the maximum attachment size, in bytes.
    /// </summary>
    public long? MaximumSizeBytes { get; init; }

    /// <summary>
    /// Gets a value indicating whether attachment URLs should be returned when supported.
    /// </summary>
    public bool ReturnUrl { get; init; }

    /// <summary>
    /// Gets a value indicating whether extra attachment metadata should be returned when supported.
    /// </summary>
    public bool ReturnMetadata { get; init; }

    /// <summary>
    /// Validates the query configuration.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the query does not contain a valid selector or contains invalid size limits.
    /// </exception>
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