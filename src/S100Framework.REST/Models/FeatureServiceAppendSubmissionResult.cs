namespace S100Framework.REST.Models;

/// <summary>
/// Represents the initial response from a service-level append submission.
/// </summary>
public sealed record FeatureServiceAppendSubmissionResult(
    string Status,
    string? StatusMessage,
    string? ItemId,
    string? JobId,
    Uri? StatusUrl,
    long? EditMoment)
{
    /// <summary>
    /// Gets a value indicating whether the append job has reached a terminal state.
    /// </summary>
    public bool IsTerminal =>
        HasStatus("Completed") ||
        HasStatus("CompletedWithErrors") ||
        HasStatus("Failed");

    /// <summary>
    /// Gets a value indicating whether the append job completed successfully enough to be treated as finished.
    /// </summary>
    public bool IsCompleted =>
        HasStatus("Completed") ||
        HasStatus("CompletedWithErrors");

    /// <summary>
    /// Gets a value indicating whether the append submission is pending and can be polled.
    /// </summary>
    public bool IsPending => StatusUrl is not null && !IsTerminal;

    private bool HasStatus(string expected) {
        return Normalize(Status) == Normalize(expected);
    }

    private static string Normalize(string value) {
        return value
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("_", string.Empty, StringComparison.Ordinal);
    }
}