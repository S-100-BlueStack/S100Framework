namespace S100Framework.REST.Models;

/// <summary>
/// Controls polling behavior for asynchronous <c>extractChanges</c> jobs.
/// </summary>
public sealed record ExtractChangesPollingOptions
{
    /// <summary>
    /// Gets the delay between status checks.
    /// </summary>
    public TimeSpan PollInterval { get; init; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Gets the maximum time to wait for the job to reach a terminal state.
    /// </summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Validates the polling configuration.
    /// </summary>
    public void Validate() {
        if (PollInterval <= TimeSpan.Zero) {
            throw new InvalidOperationException("PollInterval must be greater than zero.");
        }

        if (Timeout <= TimeSpan.Zero) {
            throw new InvalidOperationException("Timeout must be greater than zero.");
        }

        if (PollInterval >= Timeout) {
            throw new InvalidOperationException("PollInterval must be smaller than Timeout.");
        }
    }
}