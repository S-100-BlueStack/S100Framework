namespace S100Framework.REST.Models;

/// <summary>
/// Represents the initial response from an asynchronous layer-level <c>applyEdits</c> submission.
/// </summary>
/// <param name="Result">
/// The completed result when the server returns it immediately instead of continuing asynchronously.
/// </param>
/// <param name="StatusUrl">
/// The status endpoint to poll when the server continues the job asynchronously.
/// </param>
public sealed record ApplyEditsSubmissionResult(
    ApplyEditsResult? Result,
    Uri? StatusUrl)
{
    /// <summary>
    /// Gets a value indicating whether the submission is pending and should be polled through the status endpoint.
    /// </summary>
    public bool IsPending => StatusUrl is not null && Result is null;
}