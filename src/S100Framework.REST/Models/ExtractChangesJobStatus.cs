namespace S100Framework.REST.Models;

public sealed record ExtractChangesJobStatus(
    string Status,
    string? ResponseType,
    string? TransportType,
    Uri? ResultUrl,
    long? SubmissionTime,
    long? LastUpdatedTime)
{
    public bool IsTerminal =>
        Status.Equals("Completed", StringComparison.OrdinalIgnoreCase) ||
        Status.Equals("CompletedWithErrors", StringComparison.OrdinalIgnoreCase) ||
        Status.Equals("Failed", StringComparison.OrdinalIgnoreCase);

    public bool IsCompleted =>
        Status.Equals("Completed", StringComparison.OrdinalIgnoreCase) ||
        Status.Equals("CompletedWithErrors", StringComparison.OrdinalIgnoreCase);
}