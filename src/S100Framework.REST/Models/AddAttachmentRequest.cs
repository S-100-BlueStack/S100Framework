namespace S100Framework.REST.Models;

public sealed class AddAttachmentRequest
{
    public required long ObjectId { get; init; }

    public required Stream Content { get; init; }

    public required string FileName { get; init; }

    public string? ContentType { get; init; }

    public string? Keywords { get; init; }

    public bool ReturnEditMoment { get; init; }

    public string? GdbVersion { get; init; }

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