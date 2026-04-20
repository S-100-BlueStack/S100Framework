namespace S100Framework.REST.Models;

public sealed record ApplyEditsWaitOptions
{
    public TimeSpan PollInterval { get; init; } = TimeSpan.FromSeconds(1);

    public TimeSpan? Timeout { get; init; } = TimeSpan.FromMinutes(2);
}