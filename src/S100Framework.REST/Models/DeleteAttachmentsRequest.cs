namespace S100Framework.REST.Models;

/// <summary>
/// Defines a request to delete one or more attachments from a feature.
/// </summary>
public sealed record DeleteAttachmentsRequest
{
    /// <summary>
    /// Gets the object ID of the parent feature.
    /// </summary>
    public required long ObjectId { get; init; }

    /// <summary>
    /// Gets the attachment IDs to delete.
    /// </summary>
    public IReadOnlyList<long> AttachmentIds { get; init; } = Array.Empty<long>();

    /// <summary>
    /// Gets a value indicating whether the delete operation should roll back if one item fails.
    /// </summary>
    public bool RollbackOnFailure { get; init; } = true;

    /// <summary>
    /// Gets a value indicating whether the edit moment should be returned.
    /// </summary>
    public bool ReturnEditMoment { get; init; }

    /// <summary>
    /// Gets the geodatabase version to target, when applicable.
    /// </summary>
    public string? GdbVersion { get; init; }

    /// <summary>
    /// Validates the request configuration.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the request is incomplete or invalid.
    /// </exception>
    public void Validate() {
        if (ObjectId < 0) {
            throw new InvalidOperationException("ObjectId must be greater than or equal to zero.");
        }

        if (AttachmentIds.Count == 0) {
            throw new InvalidOperationException("At least one attachment ID must be provided.");
        }
    }
}