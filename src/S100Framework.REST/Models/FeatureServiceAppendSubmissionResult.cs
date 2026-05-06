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
        IsCompleted ||
        IsFailed ||
        IsCancelled ||
        IsTimedOut;

    /// <summary>
    /// Gets a value indicating whether the append job completed successfully enough to be treated as finished.
    /// </summary>
    public bool IsCompleted =>
        HasAnyStatus(
            "Completed",
            "CompletedWithErrors",
            "Completed With Errors",
            "Succeeded",
            "Success",
            "esriJobSucceeded");

    /// <summary>
    /// Gets a value indicating whether the append job failed.
    /// </summary>
    public bool IsFailed =>
        HasAnyStatus(
            "Failed",
            "Error",
            "esriJobFailed");

    /// <summary>
    /// Gets a value indicating whether the append job was cancelled.
    /// </summary>
    public bool IsCancelled =>
        HasAnyStatus(
            "Cancelled",
            "Canceled",
            "esriJobCancelled",
            "esriJobCanceled");

    /// <summary>
    /// Gets a value indicating whether the append job timed out.
    /// </summary>
    public bool IsTimedOut =>
        HasAnyStatus(
            "TimedOut",
            "Timed Out",
            "Timeout",
            "esriJobTimedOut");

    /// <summary>
    /// Gets a value indicating whether the append submission is pending and can be polled.
    /// </summary>
    public bool IsPending => StatusUrl is not null && !IsTerminal;

    private bool HasAnyStatus(params string[] expectedValues) {
        var normalizedStatus = Normalize(Status);

        return expectedValues.Any(expected =>
            string.Equals(
                normalizedStatus,
                Normalize(expected),
                StringComparison.Ordinal));
    }

    private static string Normalize(string? value) {
        return (value ?? string.Empty)
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("_", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Trim()
            .ToUpperInvariant();
    }
}