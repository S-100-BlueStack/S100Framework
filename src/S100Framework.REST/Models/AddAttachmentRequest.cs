namespace S100Framework.REST.Models;

/// <summary>
/// Defines a request to add an attachment to a feature.
/// </summary>
public sealed class AddAttachmentRequest
{
    /// <summary>
    /// Gets the object ID of the parent feature.
    /// </summary>
    public required long ObjectId { get; init; }

    /// <summary>
    /// Gets the attachment content stream.
    /// </summary>
    public required Stream Content { get; init; }

    /// <summary>
    /// Gets the attachment file name.
    /// </summary>
    public required string FileName { get; init; }

    /// <summary>
    /// Gets the attachment MIME type, when known.
    /// </summary>
    public string? ContentType { get; init; }

    /// <summary>
    /// Gets optional attachment keywords.
    /// </summary>
    public string? Keywords { get; init; }

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

        if (Content is null) {
            throw new InvalidOperationException("Content must be provided.");
        }

        if (!Content.CanRead) {
            throw new InvalidOperationException("Content stream must be readable.");
        }

        if (string.IsNullOrWhiteSpace(FileName)) {
            throw new InvalidOperationException("FileName must be provided.");
        }
    }
}