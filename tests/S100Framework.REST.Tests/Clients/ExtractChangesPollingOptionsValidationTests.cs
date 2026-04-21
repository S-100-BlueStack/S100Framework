using S100Framework.REST.Models;
using Xunit;

namespace S100Framework.REST.Tests.Clients;

public sealed class ExtractChangesPollingOptionsValidationTests
{
    [Fact]
    public void Validate_Throws_WhenPollIntervalIsZero() {
        var options = new ExtractChangesPollingOptions {
            PollInterval = TimeSpan.Zero,
            Timeout = TimeSpan.FromSeconds(1)
        };

        var exception = Assert.Throws<InvalidOperationException>(() => options.Validate());

        Assert.Contains("PollInterval must be greater than zero", exception.Message);
    }

    [Fact]
    public void Validate_Throws_WhenPollIntervalIsNegative() {
        var options = new ExtractChangesPollingOptions {
            PollInterval = TimeSpan.FromMilliseconds(-1),
            Timeout = TimeSpan.FromSeconds(1)
        };

        var exception = Assert.Throws<InvalidOperationException>(() => options.Validate());

        Assert.Contains("PollInterval must be greater than zero", exception.Message);
    }

    [Fact]
    public void Validate_Throws_WhenTimeoutIsZero() {
        var options = new ExtractChangesPollingOptions {
            PollInterval = TimeSpan.FromMilliseconds(10),
            Timeout = TimeSpan.Zero
        };

        var exception = Assert.Throws<InvalidOperationException>(() => options.Validate());

        Assert.Contains("Timeout must be greater than zero", exception.Message);
    }

    [Fact]
    public void Validate_Throws_WhenTimeoutIsNegative() {
        var options = new ExtractChangesPollingOptions {
            PollInterval = TimeSpan.FromMilliseconds(10),
            Timeout = TimeSpan.FromMilliseconds(-1)
        };

        var exception = Assert.Throws<InvalidOperationException>(() => options.Validate());

        Assert.Contains("Timeout must be greater than zero", exception.Message);
    }

    [Fact]
    public void Validate_Throws_WhenPollIntervalEqualsTimeout() {
        var options = new ExtractChangesPollingOptions {
            PollInterval = TimeSpan.FromSeconds(1),
            Timeout = TimeSpan.FromSeconds(1)
        };

        var exception = Assert.Throws<InvalidOperationException>(() => options.Validate());

        Assert.Contains("PollInterval must be smaller than Timeout", exception.Message);
    }

    [Fact]
    public void Validate_Throws_WhenPollIntervalIsGreaterThanTimeout() {
        var options = new ExtractChangesPollingOptions {
            PollInterval = TimeSpan.FromSeconds(2),
            Timeout = TimeSpan.FromSeconds(1)
        };

        var exception = Assert.Throws<InvalidOperationException>(() => options.Validate());

        Assert.Contains("PollInterval must be smaller than Timeout", exception.Message);
    }

    [Fact]
    public void Validate_DoesNotThrow_WhenOptionsAreValid() {
        var options = new ExtractChangesPollingOptions {
            PollInterval = TimeSpan.FromMilliseconds(250),
            Timeout = TimeSpan.FromSeconds(5)
        };

        options.Validate();
    }
}