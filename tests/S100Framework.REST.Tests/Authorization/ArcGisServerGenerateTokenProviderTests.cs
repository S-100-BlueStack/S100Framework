using S100Framework.REST.Authorization;
using S100Framework.REST.Exceptions;
using S100Framework.REST.Tests.TestDoubles;
using Xunit;

namespace S100Framework.REST.Tests.Authorization;

public sealed class ArcGisServerGenerateTokenProviderTests
{
    [Fact]
    public async Task GetAccessTokenAsync_PostsGenerateTokenRequestAndReturnsToken() {
        var cancellationToken = TestContext.Current.CancellationToken;

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
              "expires": 4102444800000
            }
            """);
        }));

        using var provider = new ArcGisServerGenerateTokenProvider(
            httpClient,
            new ArcGisServerGenerateTokenOptions {
                TokenUri = new Uri("https://example.test/arcgis/tokens/generateToken"),
                Username = "testUser",
                Password = "testPassword",
                ClientType = ArcGisServerTokenClientType.Referer,
                Referer = "https://consumer.test",
                ExpirationMinutes = 60
            });

        var token = await provider.GetAccessTokenAsync(cancellationToken);

        Assert.Equal("server-token-1", token.Token);
        Assert.Equal(HttpMethod.Post, method);
        Assert.Equal(
            "https://example.test/arcgis/tokens/generateToken",
            requestUri!.AbsoluteUri);
        Assert.NotNull(requestBody);
        Assert.Contains("username=testUser", requestBody!, StringComparison.Ordinal);
        Assert.Contains("password=testPassword", requestBody!, StringComparison.Ordinal);
        Assert.Contains("client=referer", requestBody!, StringComparison.Ordinal);
        Assert.Contains(
            "referer=https%3A%2F%2Fconsumer.test",
            requestBody!,
            StringComparison.Ordinal);
        Assert.Contains("expiration=60", requestBody!, StringComparison.Ordinal);
        Assert.Contains("f=json", requestBody!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetAccessTokenAsync_CachesToken_WhenItIsStillReusable() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var requestCount = 0;

        using var httpClient = new HttpClient(new StubHttpMessageHandler(_ => {
            requestCount++;

            return StubHttpMessageHandler.Json("""
            {
              "token": "cached-token",
              "expires": 4102444800000
            }
            """);
        }));

        using var provider = new ArcGisServerGenerateTokenProvider(
            httpClient,
            new ArcGisServerGenerateTokenOptions {
                TokenUri = new Uri("https://example.test/arcgis/tokens/generateToken"),
                Username = "testUser",
                Password = "testPassword",
                ClientType = ArcGisServerTokenClientType.RequestIp,
                ExpirationMinutes = 60,
                RefreshBeforeExpiration = TimeSpan.FromMinutes(1)
            });

        var first = await provider.GetAccessTokenAsync(cancellationToken);
        var second = await provider.GetAccessTokenAsync(cancellationToken);

        Assert.Equal("cached-token", first.Token);
        Assert.Equal("cached-token", second.Token);
        Assert.Equal(1, requestCount);
    }

    [Fact]
    public async Task GetAccessTokenAsync_RefreshesToken_WhenInsideRefreshWindow() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var requestCount = 0;

        using var httpClient = new HttpClient(new StubHttpMessageHandler(_ => {
            requestCount++;

            if (requestCount == 1) {
                var expiresSoon = DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeMilliseconds();

                return StubHttpMessageHandler.Json($$"""
                {
                  "token": "soon-expiring-token",
                  "expires": {{expiresSoon}}
                }
                """);
            }

            return StubHttpMessageHandler.Json("""
            {
              "token": "refreshed-token",
              "expires": 4102444800000
            }
            """);
        }));

        using var provider = new ArcGisServerGenerateTokenProvider(
            httpClient,
            new ArcGisServerGenerateTokenOptions {
                TokenUri = new Uri("https://example.test/arcgis/tokens/generateToken"),
                Username = "testUser",
                Password = "testPassword",
                ClientType = ArcGisServerTokenClientType.RequestIp,
                ExpirationMinutes = 60,
                RefreshBeforeExpiration = TimeSpan.FromMinutes(5)
            });

        var first = await provider.GetAccessTokenAsync(cancellationToken);
        var second = await provider.GetAccessTokenAsync(cancellationToken);

        Assert.Equal("soon-expiring-token", first.Token);
        Assert.Equal("refreshed-token", second.Token);
        Assert.Equal(2, requestCount);
    }

    [Fact]
    public async Task GetAccessTokenAsync_Throws_WhenServerReturnsErrorPayload() {
        var cancellationToken = TestContext.Current.CancellationToken;

        using var httpClient = new HttpClient(new StubHttpMessageHandler(_ =>
            StubHttpMessageHandler.Json("""
            {
              "error": {
                "code": 498,
                "message": "Invalid token or credentials."
              }
            }
            """)));

        using var provider = new ArcGisServerGenerateTokenProvider(
            httpClient,
            new ArcGisServerGenerateTokenOptions {
                TokenUri = new Uri("https://example.test/arcgis/tokens/generateToken"),
                Username = "testUser",
                Password = "wrongPassword",
                ClientType = ArcGisServerTokenClientType.RequestIp,
                ExpirationMinutes = 60
            });

        var exception = await Assert.ThrowsAsync<FeatureServiceAuthenticationException>(() =>
            provider.GetAccessTokenAsync(cancellationToken).AsTask());

        Assert.Contains("Invalid token or credentials", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetAccessTokenAsync_FiltersBlankErrorDetails() {
        var cancellationToken = TestContext.Current.CancellationToken;

        using var httpClient = new HttpClient(new StubHttpMessageHandler(_ =>
            StubHttpMessageHandler.Json("""
        {
          "error": {
            "code": 498,
            "message": "Invalid credentials.",
            "details": [
              null,
              "",
              "   ",
              "Password is invalid."
            ]
          }
        }
        """)));

        using var provider = new ArcGisServerGenerateTokenProvider(
            httpClient,
            new ArcGisServerGenerateTokenOptions {
                TokenUri = new Uri("https://example.test/arcgis/tokens/generateToken"),
                Username = "testUser",
                Password = "wrongPassword",
                ClientType = ArcGisServerTokenClientType.RequestIp,
                ExpirationMinutes = 60
            });

        var exception = await Assert.ThrowsAsync<FeatureServiceAuthenticationException>(() =>
            provider.GetAccessTokenAsync(cancellationToken).AsTask());

        Assert.Contains("Invalid credentials.", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Password is invalid.", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(" |  | ", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetAccessTokenAsync_UsesStableFallback_WhenErrorPayloadHasOnlyBlankDetails() {
        var cancellationToken = TestContext.Current.CancellationToken;

        using var httpClient = new HttpClient(new StubHttpMessageHandler(_ =>
            StubHttpMessageHandler.Json("""
        {
          "error": {
            "details": [
              null,
              "",
              "   "
            ]
          }
        }
        """)));

        using var provider = new ArcGisServerGenerateTokenProvider(
            httpClient,
            new ArcGisServerGenerateTokenOptions {
                TokenUri = new Uri("https://example.test/arcgis/tokens/generateToken"),
                Username = "testUser",
                Password = "wrongPassword",
                ClientType = ArcGisServerTokenClientType.RequestIp,
                ExpirationMinutes = 60
            });

        var exception = await Assert.ThrowsAsync<FeatureServiceAuthenticationException>(() =>
            provider.GetAccessTokenAsync(cancellationToken).AsTask());

        Assert.Equal(
            "The token service returned an authentication error.",
            exception.Message);
    }
}