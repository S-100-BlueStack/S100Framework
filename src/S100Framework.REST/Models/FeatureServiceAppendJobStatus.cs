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
        IsCompleted ||
        IsFailed ||
        IsCancelled ||
        IsTimedOut;

    /// <summary>
    /// Gets a value indicating whether the job completed successfully enough to be treated as finished.
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
    /// Gets a value indicating whether the job failed.
    /// </summary>
    public bool IsFailed =>
        HasAnyStatus(
            "Failed",
            "Error",
            "esriJobFailed");

    /// <summary>
    /// Gets a value indicating whether the job was cancelled.
    /// </summary>
    public bool IsCancelled =>
        HasAnyStatus(
            "Cancelled",
            "Canceled",
            "esriJobCancelled",
            "esriJobCanceled");

    /// <summary>
    /// Gets a value indicating whether the job timed out.
    /// </summary>
    public bool IsTimedOut =>
        HasAnyStatus(
            "TimedOut",
            "Timed Out",
            "Timeout",
            "esriJobTimedOut");

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