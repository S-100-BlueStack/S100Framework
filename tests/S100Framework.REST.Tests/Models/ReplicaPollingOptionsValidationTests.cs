using S100Framework.REST.Models;
using Xunit;

namespace S100Framework.REST.Tests.Models;

public sealed class ReplicaPollingOptionsValidationTests
{
    [Fact]
    public void Validate_Throws_WhenPollIntervalIsZero() {
        var options = new ReplicaPollingOptions {
            PollInterval = TimeSpan.Zero,
            Timeout = TimeSpan.FromSeconds(1)
        };

        var exception = Assert.Throws<InvalidOperationException>(() => options.Validate());

        Assert.Contains("PollInterval", exception.Message);
    }

    [Fact]
    public void Validate_Throws_WhenTimeoutIsZero() {
        var options = new ReplicaPollingOptions {
            PollInterval = TimeSpan.FromMilliseconds(1),
            Timeout = TimeSpan.Zero
        };

        var exception = Assert.Throws<InvalidOperationException>(() => options.Validate());

        Assert.Contains("Timeout", exception.Message);
    }

    [Fact]
    public void Validate_Throws_WhenPollIntervalIsNotSmallerThanTimeout() {
        var options = new ReplicaPollingOptions {
            PollInterval = TimeSpan.FromSeconds(1),
            Timeout = TimeSpan.FromSeconds(1)
        };

        var exception = Assert.Throws<InvalidOperationException>(() => options.Validate());

        Assert.Contains("smaller than Timeout", exception.Message);
    }

    [Fact]
    public void Validate_DoesNotThrow_ForValidOptions() {
        var options = new ReplicaPollingOptions {
            PollInterval = TimeSpan.FromMilliseconds(1),
            Timeout = TimeSpan.FromSeconds(1)
        };

        options.Validate();
    }
}