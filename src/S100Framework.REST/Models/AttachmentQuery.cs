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
    /// At least one of <see cref="ObjectIds"/>, <see cref="GlobalIds"/>, or
    /// <see cref="DefinitionExpression"/> must be provided.
    /// </remarks>
    public IReadOnlyList<long>? ObjectIds { get; init; }

    /// <summary>
    /// Gets the global IDs to match.
    /// </summary>
    /// <remarks>
    /// At least one of <see cref="ObjectIds"/>, <see cref="GlobalIds"/>, or
    /// <see cref="DefinitionExpression"/> must be provided.
    /// </remarks>
    public IReadOnlyList<string>? GlobalIds { get; init; }

    /// <summary>
    /// Gets the layer definition expression used to select parent features.
    /// </summary>
    /// <remarks>
    /// At least one of <see cref="ObjectIds"/>, <see cref="GlobalIds"/>, or
    /// <see cref="DefinitionExpression"/> must be provided.
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
    /// Gets the zero-based attachment result offset to request from the service.
    /// </summary>
    public int? ResultOffset { get; init; }

    /// <summary>
    /// Gets the maximum number of attachment records to return.
    /// </summary>
    public int? ResultRecordCount { get; init; }

    /// <summary>
    /// Gets the attachment order-by fields.
    /// </summary>
    /// <remarks>
    /// This value is sent as the ArcGIS REST <c>orderByFields</c> parameter for attachment queries.
    /// </remarks>
    public IReadOnlyList<string>? OrderByFields { get; init; }

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
    /// Thrown when the query does not contain a valid selector or contains invalid size or paging limits.
    /// </exception>
    public void Validate() {
        var hasObjectIds = ObjectIds is { Count: > 0 };
        var hasGlobalIds = GlobalIds is { Count: > 0 };
        var hasDefinitionExpression = !string.IsNullOrWhiteSpace(DefinitionExpression);

        if (!hasObjectIds && !hasGlobalIds && !hasDefinitionExpression) {
            throw new InvalidOperationException(
                "At least one of ObjectIds, GlobalIds, or DefinitionExpression must be provided.");
        }

        if (hasObjectIds && hasGlobalIds) {
            throw new InvalidOperationException("ObjectIds and GlobalIds cannot both be specified.");
        }

        if (ObjectIds is { Count: 0 }) {
            throw new InvalidOperationException("ObjectIds must not be empty when provided.");
        }

        if (GlobalIds is { Count: 0 }) {
            throw new InvalidOperationException("GlobalIds must not be empty when provided.");
        }

        if (GlobalIds?.Any(string.IsNullOrWhiteSpace) == true) {
            throw new InvalidOperationException("GlobalIds must not contain empty values.");
        }

        if (DefinitionExpression is not null && string.IsNullOrWhiteSpace(DefinitionExpression)) {
            throw new InvalidOperationException("DefinitionExpression must not be empty when provided.");
        }

        if (ResultOffset is < 0) {
            throw new InvalidOperationException("ResultOffset must be greater than or equal to zero when provided.");
        }

        if (ResultRecordCount is <= 0) {
            throw new InvalidOperationException("ResultRecordCount must be greater than zero when provided.");
        }

        if (OrderByFields is { Count: 0 }) {
            throw new InvalidOperationException("OrderByFields must not be empty when provided.");
        }

        if (OrderByFields?.Any(string.IsNullOrWhiteSpace) == true) {
            throw new InvalidOperationException("OrderByFields must not contain empty values.");
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