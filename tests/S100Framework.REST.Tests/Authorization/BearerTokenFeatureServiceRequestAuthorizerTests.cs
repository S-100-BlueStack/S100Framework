using S100Framework.REST.Abstractions;
using S100Framework.REST.Authorization;
using Xunit;

namespace S100Framework.REST.Tests.Authorization;

public sealed class BearerTokenFeatureServiceRequestAuthorizerTests
{
    [Fact]
    public async Task ApplyAsync_SetsBearerHeader_WhenUsingAccessTokenProvider() {
        var authorizer = new BearerTokenFeatureServiceRequestAuthorizer(
            new FakeAccessTokenProvider("provider-token"));

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "https://example.test/arcgis/rest/services/Test/FeatureServer");

        await authorizer.ApplyAsync(request);

        Assert.NotNull(request.Headers.Authorization);
        Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
        Assert.Equal("provider-token", request.Headers.Authorization.Parameter);
    }

    private sealed class FakeAccessTokenProvider : IFeatureServiceAccessTokenProvider
    {
        private readonly string _token;

        public FakeAccessTokenProvider(string token) {
            _token = token;
        }

        public ValueTask<FeatureServiceAccessToken> GetAccessTokenAsync(
            CancellationToken cancellationToken = default) {
            return ValueTask.FromResult(
                new FeatureServiceAccessToken(
                    _token,
                    DateTimeOffset.UtcNow.AddHours(1)));
        }
    }
}