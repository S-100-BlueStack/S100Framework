namespace S100Framework.REST.Models;

/// <summary>
/// Controls polling behavior for asynchronous <c>calculate</c> jobs.
/// </summary>
public sealed record CalculateWaitOptions
{
    /// <summary>
    /// Gets the delay between status checks.
    /// </summary>
    public TimeSpan PollInterval { get; init; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Gets the maximum time to wait for the job to complete.
    /// </summary>
    /// <remarks>
    /// Set this value to <see langword="null" /> to wait indefinitely.
    /// </remarks>
    public TimeSpan? Timeout { get; init; } = TimeSpan.FromMinutes(2);
}