using S100Framework.REST.Authorization;
using Xunit;

namespace S100Framework.REST.Tests.Authorization;

public sealed class StaticFeatureServiceAccessTokenProviderTests
{
    [Fact]
    public async Task GetAccessTokenAsync_ReturnsConfiguredToken() {
        var provider = new StaticFeatureServiceAccessTokenProvider(
            new FeatureServiceAccessToken(
                "static-token",
                DateTimeOffset.UtcNow.AddHours(1)));

        var token = await provider.GetAccessTokenAsync();

        Assert.Equal("static-token", token.Token);
    }

    [Fact]
    public void Constructor_Throws_WhenTokenIsWhitespace() {
        var exception = Assert.Throws<ArgumentException>(() =>
            new StaticFeatureServiceAccessTokenProvider(" "));

        Assert.Contains("token", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}