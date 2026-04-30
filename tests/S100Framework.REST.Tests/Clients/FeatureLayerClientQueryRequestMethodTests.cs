using System.Net.Http;
using S100Framework.REST.Clients;
using S100Framework.REST.Configuration;
using S100Framework.REST.Models;
using S100Framework.REST.Tests.TestDoubles;
using Xunit;

namespace S100Framework.REST.Tests.Clients;

public sealed class FeatureLayerClientQueryRequestMethodTests
{
    [Fact]
    public async Task QueryCountAsync_UsesGet_WhenPreferenceIsGet() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var queryRequests = new List<HttpRequestMessage>();

        var client = CreateClient(
            QueryRequestMethodPreference.Get,
            autoPostQueryLengthThreshold: 1,
            request => {
                if (IsLayerQueryRequest(request)) {
                    queryRequests.Add(request);

                    return StubHttpMessageHandler.Json("""
                    {
                      "count": 7
                    }
                    """);
                }

                throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
            });

        var count = await client.GetLayerClient(0).QueryCountAsync(
            new FeatureQuery {
                Where = "STATUS = 'Active'"
            },
            cancellationToken);

        Assert.Equal(7, count);

        var request = Assert.Single(queryRequests);

        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Null(request.Content);
        Assert.Contains("returnCountOnly=true", request.RequestUri!.Query, StringComparison.Ordinal);
        Assert.Contains("where=STATUS", Uri.UnescapeDataString(request.RequestUri.Query), StringComparison.Ordinal);
    }

    [Fact]
    public async Task QueryCountAsync_UsesPostForm_WhenPreferenceIsPost() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var queryRequests = new List<HttpRequestMessage>();
        var formBodies = new List<string>();

        var client = CreateClient(
            QueryRequestMethodPreference.Post,
            autoPostQueryLengthThreshold: 10_000,
            request => {
                if (IsLayerQueryRequest(request)) {
                    queryRequests.Add(request);
                    formBodies.Add(ReadRequestBody(request));

                    return StubHttpMessageHandler.Json("""
                    {
                      "count": 11
                    }
                    """);
                }

                throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
            });

        var count = await client.GetLayerClient(0).QueryCountAsync(
            new FeatureQuery {
                Where = "STATUS = 'Active'"
            },
            cancellationToken);

        Assert.Equal(11, count);

        var request = Assert.Single(queryRequests);
        var form = ParseFormBody(Assert.Single(formBodies));

        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal(string.Empty, request.RequestUri!.Query);
        Assert.NotNull(request.Content);
        Assert.Equal("application/x-www-form-urlencoded", request.Content.Headers.ContentType?.MediaType);
        Assert.Equal("true", form["returnCountOnly"]);
        Assert.Equal("STATUS = 'Active'", form["where"]);
    }

    [Fact]
    public async Task QueryCountAsync_UsesPostForm_WhenAutoThresholdIsExceeded() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var queryRequests = new List<HttpRequestMessage>();
        var formBodies = new List<string>();

        var client = CreateClient(
            QueryRequestMethodPreference.Auto,
            autoPostQueryLengthThreshold: 120,
            request => {
                if (IsLayerQueryRequest(request)) {
                    queryRequests.Add(request);
                    formBodies.Add(ReadRequestBody(request));

                    return StubHttpMessageHandler.Json("""
                    {
                      "count": 13
                    }
                    """);
                }

                throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
            });

        var count = await client.GetLayerClient(0).QueryCountAsync(
            new FeatureQuery {
                Where = $"NAME IN ({string.Join(",", Enumerable.Range(0, 50).Select(value => $"'{value:D3}'"))})"
            },
            cancellationToken);

        Assert.Equal(13, count);

        var request = Assert.Single(queryRequests);
        var form = ParseFormBody(Assert.Single(formBodies));

        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal(string.Empty, request.RequestUri!.Query);
        Assert.Equal("true", form["returnCountOnly"]);
        Assert.StartsWith("NAME IN", form["where"], StringComparison.Ordinal);
    }

    [Fact]
    public async Task QueryCountAsync_UsesGet_WhenAutoThresholdIsNotExceeded() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var queryRequests = new List<HttpRequestMessage>();

        var client = CreateClient(
            QueryRequestMethodPreference.Auto,
            autoPostQueryLengthThreshold: 10_000,
            request => {
                if (IsLayerQueryRequest(request)) {
                    queryRequests.Add(request);

                    return StubHttpMessageHandler.Json("""
                    {
                      "count": 17
                    }
                    """);
                }

                throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
            });

        var count = await client.GetLayerClient(0).QueryCountAsync(
            new FeatureQuery {
                Where = "OBJECTID > 0"
            },
            cancellationToken);

        Assert.Equal(17, count);

        var request = Assert.Single(queryRequests);

        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Null(request.Content);
        Assert.Contains("returnCountOnly=true", request.RequestUri!.Query, StringComparison.Ordinal);
        Assert.Contains("where=OBJECTID", Uri.UnescapeDataString(request.RequestUri.Query), StringComparison.Ordinal);
    }

    private static FeatureServiceClient CreateClient(
        QueryRequestMethodPreference queryRequestMethodPreference,
        int autoPostQueryLengthThreshold,
        Func<HttpRequestMessage, HttpResponseMessage> handler) {
        return new FeatureServiceClient(
            new HttpClient(new StubHttpMessageHandler(handler)),
            new FeatureServiceClientOptions {
                ServiceUri = new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer"),
                QueryRequestMethodPreference = queryRequestMethodPreference,
                AutoPostQueryLengthThreshold = autoPostQueryLengthThreshold
            });
    }

    private static bool IsLayerQueryRequest(HttpRequestMessage request) {
        return request.RequestUri?.AbsolutePath.EndsWith(
            "/FeatureServer/0/query",
            StringComparison.OrdinalIgnoreCase) == true;
    }

    private static string ReadRequestBody(HttpRequestMessage request) {
        return request.Content is null
            ? string.Empty
            : request.Content.ReadAsStringAsync().GetAwaiter().GetResult();
    }

    private static Dictionary<string, string> ParseFormBody(string body) {
        return body
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Split('=', 2))
            .ToDictionary(
                parts => DecodeFormValue(parts[0]),
                parts => parts.Length == 2 ? DecodeFormValue(parts[1]) : string.Empty,
                StringComparer.Ordinal);
    }

    private static string DecodeFormValue(string value) {
        return Uri.UnescapeDataString(value.Replace("+", "%20", StringComparison.Ordinal));
    }
}