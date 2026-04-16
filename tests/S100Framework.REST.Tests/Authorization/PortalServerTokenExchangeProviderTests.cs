using S100Framework.REST.Abstractions;
using S100Framework.REST.Authorization;
using S100Framework.REST.Exceptions;
using S100Framework.REST.Tests.TestDoubles;
using Xunit;

namespace S100Framework.REST.Tests.Authorization;

public sealed class PortalServerTokenExchangeProviderTests
{
    [Fact]
    public async Task GetAccessTokenAsync_PostsPortalTokenAndServerUrlAndReturnsServerToken() {
        string? requestBody = null;
        HttpMethod? method = null;
        Uri? requestUri = null;

        using var httpClient = new HttpClient(new StubHttpMessageHandler(request => {
            method = request.Method;
            requestUri = request.RequestUri;
            requestBody = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();

            return StubHttpMessageHandler.Json("""
            {
              "token": "server-token-1",
              "expires": 4102444800000,
              "ssl": true
            }
            """);
        }));

        using var provider = new PortalServerTokenExchangeProvider(
            httpClient,
            new FakePortalAccessTokenProvider("portal-token-1"),
            new PortalServerTokenExchangeOptions {
                GenerateTokenUri = new Uri("https://portal.example.com/portal/sharing/rest/generateToken"),
                ServerUrl = new Uri("https://server.example.com/server")
            });

        var token = await provider.GetAccessTokenAsync();

        Assert.Equal("server-token-1", token.Token);
        Assert.Equal(HttpMethod.Post, method);
        Assert.Equal(
            "https://portal.example.com/portal/sharing/rest/generateToken",
            requestUri!.AbsoluteUri);

        Assert.NotNull(requestBody);
        Assert.Contains("token=portal-token-1", requestBody!, StringComparison.Ordinal);
        Assert.Contains(
            "serverUrl=https%3A%2F%2Fserver.example.com%2Fserver",
            requestBody!,
            StringComparison.Ordinal);
        Assert.Contains("f=json", requestBody!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetAccessTokenAsync_CachesServerToken_WhenItIsStillReusable() {
        var requestCount = 0;

        using var httpClient = new HttpClient(new StubHttpMessageHandler(_ => {
            requestCount++;

            return StubHttpMessageHandler.Json("""
            {
              "token": "server-token-cached",
              "expires": 4102444800000
            }
            """);
        }));

        using var provider = new PortalServerTokenExchangeProvider(
            httpClient,
            new FakePortalAccessTokenProvider("portal-token-1"),
            new PortalServerTokenExchangeOptions {
                GenerateTokenUri = new Uri("https://portal.example.com/portal/sharing/rest/generateToken"),
                ServerUrl = new Uri("https://server.example.com/server"),
                RefreshBeforeExpiration = TimeSpan.FromMinutes(1)
            });

        var first = await provider.GetAccessTokenAsync();
        var second = await provider.GetAccessTokenAsync();

        Assert.Equal("server-token-cached", first.Token);
        Assert.Equal("server-token-cached", second.Token);
        Assert.Equal(1, requestCount);
    }

    [Fact]
    public async Task GetAccessTokenAsync_RefreshesServerToken_WhenInsideRefreshWindow() {
        var requestCount = 0;

        using var httpClient = new HttpClient(new StubHttpMessageHandler(_ => {
            requestCount++;

            if (requestCount == 1) {
                var expiresSoon = DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeMilliseconds();

                return StubHttpMessageHandler.Json($$"""
                {
                  "token": "soon-expiring-server-token",
                  "expires": {{expiresSoon}}
                }
                """);
            }

            return StubHttpMessageHandler.Json("""
            {
              "token": "refreshed-server-token",
              "expires": 4102444800000
            }
            """);
        }));

        using var provider = new PortalServerTokenExchangeProvider(
            httpClient,
            new FakePortalAccessTokenProvider("portal-token-1"),
            new PortalServerTokenExchangeOptions {
                GenerateTokenUri = new Uri("https://portal.example.com/portal/sharing/rest/generateToken"),
                ServerUrl = new Uri("https://server.example.com/server"),
                RefreshBeforeExpiration = TimeSpan.FromMinutes(5)
            });

        var first = await provider.GetAccessTokenAsync();
        var second = await provider.GetAccessTokenAsync();

        Assert.Equal("soon-expiring-server-token", first.Token);
        Assert.Equal("refreshed-server-token", second.Token);
        Assert.Equal(2, requestCount);
    }

    [Fact]
    public async Task GetAccessTokenAsync_Throws_WhenPortalReturnsErrorPayload() {
        using var httpClient = new HttpClient(new StubHttpMessageHandler(_ =>
            StubHttpMessageHandler.Json("""
            {
              "error": {
                "code": 498,
                "message": "Invalid token."
              }
            }
            """)));

        using var provider = new PortalServerTokenExchangeProvider(
            httpClient,
            new FakePortalAccessTokenProvider("bad-portal-token"),
            new PortalServerTokenExchangeOptions {
                GenerateTokenUri = new Uri("https://portal.example.com/portal/sharing/rest/generateToken"),
                ServerUrl = new Uri("https://server.example.com/server")
            });

        var exception = await Assert.ThrowsAsync<FeatureServiceAuthenticationException>(() =>
            provider.GetAccessTokenAsync().AsTask());

        Assert.Contains("Invalid token", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetAccessTokenAsync_UsesLatestPortalToken_WhenRefreshing() {
        var requestCount = 0;
        string? lastRequestBody = null;

        using var httpClient = new HttpClient(new StubHttpMessageHandler(request => {
            requestCount++;
            lastRequestBody = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();

            if (requestCount == 1) {
                var expiresSoon = DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeMilliseconds();

                return StubHttpMessageHandler.Json($$"""
                {
                  "token": "server-token-1",
                  "expires": {{expiresSoon}}
                }
                """);
            }

            return StubHttpMessageHandler.Json("""
            {
              "token": "server-token-2",
              "expires": 4102444800000
            }
            """);
        }));

        var portalTokenProvider = new RotatingPortalAccessTokenProvider(
            "portal-token-1",
            "portal-token-2");

        using var provider = new PortalServerTokenExchangeProvider(
            httpClient,
            portalTokenProvider,
            new PortalServerTokenExchangeOptions {
                GenerateTokenUri = new Uri("https://portal.example.com/portal/sharing/rest/generateToken"),
                ServerUrl = new Uri("https://server.example.com/server"),
                RefreshBeforeExpiration = TimeSpan.FromMinutes(5)
            });

        await provider.GetAccessTokenAsync();
        await provider.GetAccessTokenAsync();

        Assert.Equal(2, requestCount);
        Assert.NotNull(lastRequestBody);
        Assert.Contains("token=portal-token-2", lastRequestBody!, StringComparison.Ordinal);
    }

    private sealed class FakePortalAccessTokenProvider : IFeatureServiceAccessTokenProvider
    {
        private readonly string _token;

        public FakePortalAccessTokenProvider(string token) {
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

    private sealed class RotatingPortalAccessTokenProvider : IFeatureServiceAccessTokenProvider
    {
        private readonly Queue<string> _tokens;

        public RotatingPortalAccessTokenProvider(params string[] tokens) {
            _tokens = new Queue<string>(tokens);
        }

        public ValueTask<FeatureServiceAccessToken> GetAccessTokenAsync(
            CancellationToken cancellationToken = default) {
            if (_tokens.Count == 0) {
                throw new InvalidOperationException("No more portal tokens were configured.");
            }

            return ValueTask.FromResult(
                new FeatureServiceAccessToken(
                    _tokens.Dequeue(),
                    DateTimeOffset.UtcNow.AddHours(1)));
        }
    }
}