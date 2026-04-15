namespace S100Framework.REST.Models;

public sealed record ExtractChangesPollingOptions
{
    public TimeSpan PollInterval { get; init; } = TimeSpan.FromSeconds(2);

    public TimeSpan Timeout { get; init; } = TimeSpan.FromMinutes(2);

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