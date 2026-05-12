namespace S100Framework.REST.Models;

/// <summary>
/// Controls polling behavior for asynchronous replica jobs.
/// </summary>
public sealed record ReplicaPollingOptions
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
    /// <exception cref="InvalidOperationException">
    /// Thrown when the configured timing values are invalid.
    /// </exception>
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