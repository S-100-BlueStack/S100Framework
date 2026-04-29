namespace S100Framework.REST.Models;

/// <summary>
/// Represents the current state of an asynchronous <c>calculate</c> job.
/// </summary>
/// <param name="Status">
/// The raw job status returned by the feature service.
/// </param>
/// <param name="RecordCount">
/// The number of records updated by the job, when returned by the service.
/// </param>
/// <param name="SubmissionTime">
/// The server submission timestamp, when available.
/// </param>
/// <param name="LastUpdatedTime">
/// The server last-updated timestamp, when available.
/// </param>
public sealed record CalculateJobStatus(
    string Status,
    long? RecordCount,
    long? SubmissionTime,
    long? LastUpdatedTime)
{
    /// <summary>
    /// Gets a value indicating whether the job is still waiting, running, executing, or being cancelled.
    /// </summary>
    public bool IsRunning =>
        HasAnyStatus(
            "Submitted",
            "Waiting",
            "Executing",
            "InProgress",
            "Processing",
            "Running",
            "Cancelling",
            "esriJobSubmitted",
            "esriJobWaiting",
            "esriJobExecuting",
            "esriJobCancelling");

    /// <summary>
    /// Gets a value indicating whether the job completed successfully.
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

    /// <summary>
    /// Gets a value indicating whether the job has reached a terminal state.
    /// </summary>
    public bool IsTerminal =>
        IsCompleted ||
        IsFailed ||
        IsCancelled ||
        IsTimedOut;

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