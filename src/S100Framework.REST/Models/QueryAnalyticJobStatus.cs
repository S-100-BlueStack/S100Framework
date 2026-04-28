namespace S100Framework.REST.Models;

/// <summary>
/// Represents the current state of an asynchronous <c>queryAnalytic</c> job.
/// </summary>
/// <param name="Status">
/// The raw job status returned by the feature service.
/// </param>
/// <param name="ResultUrl">
/// The result endpoint when the job has produced output.
/// </param>
/// <param name="SubmissionTime">
/// The server submission timestamp, when available.
/// </param>
/// <param name="LastUpdatedTime">
/// The server last-updated timestamp, when available.
/// </param>
public sealed record QueryAnalyticJobStatus(
    string Status,
    Uri? ResultUrl,
    long? SubmissionTime,
    long? LastUpdatedTime)
{
    /// <summary>
    /// Gets a value indicating whether the job has reached a terminal state.
    /// </summary>
    public bool IsTerminal =>
        HasStatus("Completed") ||
        HasStatus("CompletedWithErrors") ||
        HasStatus("Completed With Errors") ||
        HasStatus("Failed");

    /// <summary>
    /// Gets a value indicating whether the job completed and can return a result payload.
    /// </summary>
    public bool IsCompleted =>
        HasStatus("Completed") ||
        HasStatus("CompletedWithErrors") ||
        HasStatus("Completed With Errors");

    private bool HasStatus(string expected) {
        return string.Equals(
            Normalize(Status),
            Normalize(expected),
            StringComparison.Ordinal);
    }

    private static string Normalize(string value) {
        return (value ?? string.Empty)
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("_", string.Empty, StringComparison.Ordinal)
            .Trim()
            .ToUpperInvariant();
    }
}