namespace S100Framework.REST.Models;

public sealed record DeleteAttachmentsRequest
{
    public required long ObjectId { get; init; }

    public IReadOnlyList<long> AttachmentIds { get; init; } = Array.Empty<long>();

    public bool RollbackOnFailure { get; init; } = true;

    public bool ReturnEditMoment { get; init; }

    public string? GdbVersion { get; init; }

    public void Validate() {
        if (ObjectId < 0) {
            throw new InvalidOperationException("ObjectId must be greater than or equal to zero.");
        }

        if (AttachmentIds.Count == 0) {
            throw new InvalidOperationException("At least one attachment ID must be provided.");
        }
    }
}