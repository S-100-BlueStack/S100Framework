namespace S100Framework.REST.Models;

/// <summary>
/// Represents a file upload request for the feature service uploads endpoint.
/// </summary>
public sealed record FeatureServiceUploadRequest
{
    /// <summary>
    /// Gets the file content to upload.
    /// </summary>
    public Stream? Content { get; init; }

    /// <summary>
    /// Gets the file name sent to the server.
    /// </summary>
    public string FileName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the content type sent with the file part, when known.
    /// </summary>
    public string? ContentType { get; init; }

    /// <summary>
    /// Gets the optional upload description sent to the server.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Validates the upload request before it is sent.
    /// </summary>
    public void Validate() {
        if (Content is null) {
            throw new InvalidOperationException("Content must be provided.");
        }

        if (!Content.CanRead) {
            throw new InvalidOperationException("Content must be readable.");
        }

        if (string.IsNullOrWhiteSpace(FileName)) {
            throw new InvalidOperationException("FileName must be provided.");
        }

        if (ContentType is not null && string.IsNullOrWhiteSpace(ContentType)) {
            throw new InvalidOperationException("ContentType must not be empty when provided.");
        }

        if (Description is not null && string.IsNullOrWhiteSpace(Description)) {
            throw new InvalidOperationException("Description must not be empty when provided.");
        }
    }
}