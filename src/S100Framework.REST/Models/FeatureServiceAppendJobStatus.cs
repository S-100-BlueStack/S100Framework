namespace S100Framework.REST.Models;

/// <summary>
/// Represents the current state of an asynchronous append job.
/// </summary>
public sealed record FeatureServiceAppendJobStatus(
    string Status,
    string? LayerName,
    long? RecordCount,
    long? SubmissionTime,
    long? LastUpdatedTime,
    long? EditMoment)
{
    /// <summary>
    /// Gets a value indicating whether the job has reached a terminal state.
    /// </summary>
    public bool IsTerminal =>
        HasStatus("Completed") ||
        HasStatus("CompletedWithErrors") ||
        HasStatus("Failed");

    /// <summary>
    /// Gets a value indicating whether the job completed successfully enough to be treated as finished.
    /// </summary>
    public bool IsCompleted =>
        HasStatus("Completed") ||
        HasStatus("CompletedWithErrors");

    private bool HasStatus(string expected) {
        return string.Equals(
            Normalize(Status),
            Normalize(expected),
            StringComparison.Ordinal);
    }

    private static string Normalize(string? value) {
        return (value ?? string.Empty)
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("_", string.Empty, StringComparison.Ordinal)
            .Trim()
            .ToUpperInvariant();
    }
}