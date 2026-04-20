namespace S100Framework.REST.Models;

public sealed record ApplyEditsJobStatus(
    string Status,
    Uri? ResultUrl,
    long? SubmissionTime,
    long? LastUpdatedTime)
{
    public bool IsTerminal =>
        HasStatus("Completed") ||
        HasStatus("CompletedWithErrors") ||
        HasStatus("Failed");

    public bool IsCompleted =>
        HasStatus("Completed") ||
        HasStatus("CompletedWithErrors");

    private bool HasStatus(string expected) {
        return Normalize(Status) == Normalize(expected);
    }

    private static string Normalize(string value) {
        return value
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("_", string.Empty, StringComparison.Ordinal);
    }
}