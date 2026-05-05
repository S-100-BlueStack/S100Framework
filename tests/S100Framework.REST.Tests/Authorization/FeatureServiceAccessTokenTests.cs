using S100Framework.REST.Authorization;
using Xunit;

namespace S100Framework.REST.Tests.Authorization;

public sealed class FeatureServiceAccessTokenTests
{
    [Fact]
    public void IsExpired_ReturnsTrue_WhenNowIsAtExpiration() {
        var expiresAtUtc = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var token = new FeatureServiceAccessToken("token", expiresAtUtc);

        Assert.True(token.IsExpired(expiresAtUtc));
    }

    [Fact]
    public void IsExpired_ReturnsFalse_WhenNowIsBeforeExpiration() {
        var expiresAtUtc = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var token = new FeatureServiceAccessToken("token", expiresAtUtc);

        Assert.False(token.IsExpired(expiresAtUtc.AddTicks(-1)));
    }

    [Fact]
    public void ShouldRefresh_ReturnsFalse_WhenTokenIsOutsideRefreshWindow() {
        var utcNow = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var token = new FeatureServiceAccessToken(
            "token",
            utcNow.AddMinutes(10));

        Assert.False(token.ShouldRefresh(utcNow, TimeSpan.FromMinutes(5)));
    }

    [Fact]
    public void ShouldRefresh_ReturnsTrue_WhenTokenIsInsideRefreshWindow() {
        var utcNow = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var token = new FeatureServiceAccessToken(
            "token",
            utcNow.AddMinutes(4));

        Assert.True(token.ShouldRefresh(utcNow, TimeSpan.FromMinutes(5)));
    }

    [Fact]
    public void ShouldRefresh_ReturnsExpiredState_WhenRefreshWindowIsZero() {
        var utcNow = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var token = new FeatureServiceAccessToken(
            "token",
            utcNow.AddTicks(1));

        Assert.False(token.ShouldRefresh(utcNow, TimeSpan.Zero));
        Assert.True(token.ShouldRefresh(utcNow.AddTicks(1), TimeSpan.Zero));
    }

    [Fact]
    public void ShouldRefresh_ReturnsTrue_WhenRefreshWindowWouldUnderflowExpiration() {
        var token = new FeatureServiceAccessToken(
            "token",
            DateTimeOffset.MinValue);

        Assert.True(token.ShouldRefresh(DateTimeOffset.MinValue, TimeSpan.FromTicks(1)));
    }

    [Fact]
    public void ShouldRefresh_Throws_WhenRefreshWindowIsNegative() {
        var token = new FeatureServiceAccessToken(
            "token",
            DateTimeOffset.UtcNow.AddHours(1));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            token.ShouldRefresh(
                DateTimeOffset.UtcNow,
                TimeSpan.FromTicks(-1)));

        Assert.Contains("RefreshBeforeExpiration", exception.Message, StringComparison.Ordinal);
    }
}