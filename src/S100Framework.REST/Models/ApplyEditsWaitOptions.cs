namespace S100Framework.REST.Models;

/// <summary>
/// Controls polling behavior for asynchronous <c>applyEdits</c> jobs.
/// </summary>
public sealed record ApplyEditsWaitOptions
{
    /// <summary>
    /// Gets the delay between status checks.
    /// </summary>
    public TimeSpan PollInterval { get; init; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Gets the maximum time to wait for the job to reach a terminal state.
    /// </summary>
    /// <remarks>
    /// Set this value to <see langword="null" /> to wait indefinitely.
    /// </remarks>
    public TimeSpan? Timeout { get; init; } = TimeSpan.FromMinutes(2);
}