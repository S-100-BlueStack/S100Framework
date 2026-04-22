namespace S100Framework.REST.Models;

/// <summary>
/// Represents the current state of an asynchronous <c>extractChanges</c> job.
/// </summary>
/// <param name="Status">
/// The raw job status returned by the feature service.
/// </param>
/// <param name="ResponseType">
/// The response type returned by the job, when available.
/// </param>
/// <param name="TransportType">
/// The transport type returned by the job, when available.
/// </param>
/// <param name="ResultUrl">
/// The result URL for a completed job, when available.
/// </param>
/// <param name="SubmissionTime">
/// The server submission timestamp, when available.
/// </param>
/// <param name="LastUpdatedTime">
/// The server last-updated timestamp, when available.
/// </param>
public sealed record ExtractChangesJobStatus(
    string Status,
    string? ResponseType,
    string? TransportType,
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
        HasStatus("Failed");

    /// <summary>
    /// Gets a value indicating whether the job completed and can expose a result payload.
    /// </summary>
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