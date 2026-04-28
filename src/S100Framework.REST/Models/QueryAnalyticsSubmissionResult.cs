namespace S100Framework.REST.Models;

/// <summary>
/// Represents the initial response from an asynchronous <c>queryAnalytic</c> submission.
/// </summary>
/// <param name="Result">
/// The immediate result payload, when the service returns a completed result directly.
/// </param>
/// <param name="StatusUrl">
/// The status URL that can be polled when the operation is running asynchronously.
/// </param>
public sealed record QueryAnalyticSubmissionResult(
    QueryAnalyticResult? Result,
    Uri? StatusUrl)
{
    /// <summary>
    /// Gets a value indicating whether the submission returned a status URL and should be polled.
    /// </summary>
    public bool IsPending => Result is null && StatusUrl is not null;
}