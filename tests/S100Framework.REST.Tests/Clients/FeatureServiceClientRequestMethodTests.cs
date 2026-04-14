using S100Framework.REST.Clients;
using S100Framework.REST.Configuration;
using S100Framework.REST.Models;
using S100Framework.REST.Tests.TestDoubles;
using System.Net.Http;
using Xunit;

namespace S100Framework.REST.Tests.Clients;

public sealed class FeatureServiceClientRequestMethodTests
{
    [Fact]
    public async Task QueryCountAsync_UsesGet_WhenAutoAndRequestIsShort() {
        HttpMethod? method = null;
        string? requestUri = null;
        string? requestBody = null;

        var handler = new StubHttpMessageHandler(request => {
            method = request.Method;
            requestUri = request.RequestUri!.AbsoluteUri;
            requestBody = request.Content is null
                ? null
                : request.Content.ReadAsStringAsync().GetAwaiter().GetResult();

            return StubHttpMessageHandler.Json("""
            {
              "count": 5
            }
            """);
        });

        var client = new FeatureServiceClient(
            new HttpClient(handler),
            new FeatureServiceClientOptions {
                ServiceUri = new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer"),
                QueryRequestMethodPreference = QueryRequestMethodPreference.Auto,
                AutoPostQueryLengthThreshold = 5000
            });

        var count = await client.GetLayerClient(0).QueryCountAsync(new FeatureQuery());

        Assert.Equal(5, count);
        Assert.Equal(HttpMethod.Get, method);
        Assert.NotNull(requestUri);
        Assert.Contains("/FeatureServer/0/query?", requestUri);
        Assert.Null(requestBody);
    }

    [Fact]
    public async Task QueryCountAsync_UsesPost_WhenPreferenceIsPost() {
        HttpMethod? method = null;
        string? requestUri = null;
        string? requestBody = null;

        var handler = new StubHttpMessageHandler(request => {
            method = request.Method;
            requestUri = request.RequestUri!.AbsoluteUri;
            requestBody = request.Content is null
                ? null
                : request.Content.ReadAsStringAsync().GetAwaiter().GetResult();

            return StubHttpMessageHandler.Json("""
            {
              "count": 7
            }
            """);
        });

        var client = new FeatureServiceClient(
            new HttpClient(handler),
            new FeatureServiceClientOptions {
                ServiceUri = new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer"),
                QueryRequestMethodPreference = QueryRequestMethodPreference.Post
            });

        var count = await client.GetLayerClient(0).QueryCountAsync(new FeatureQuery());

        Assert.Equal(7, count);
        Assert.Equal(HttpMethod.Post, method);
        Assert.Equal("https://example.test/arcgis/rest/services/Test/FeatureServer/0/query", requestUri);
        Assert.NotNull(requestBody);
        Assert.Contains("f=json", requestBody);
        Assert.Contains("returnCountOnly=true", requestBody);
        Assert.Contains("where=1%3D1", requestBody);
    }

    [Fact]
    public async Task QueryStatisticsAsync_UsesPost_WhenAutoAndRequestBecomesLong() {
        HttpMethod? method = null;
        string? requestUri = null;
        string? requestBody = null;

        var handler = new StubHttpMessageHandler(request => {
            method = request.Method;
            requestUri = request.RequestUri!.AbsoluteUri;
            requestBody = request.Content is null
                ? null
                : request.Content.ReadAsStringAsync().GetAwaiter().GetResult();

            return StubHttpMessageHandler.Json("""
            {
              "features": [
                {
                  "attributes": {
                    "PLANNAME": "Plan A",
                    "FEATURE_COUNT": 2
                  }
                }
              ]
            }
            """);
        });

        var client = new FeatureServiceClient(
            new HttpClient(handler),
            new FeatureServiceClientOptions {
                ServiceUri = new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer"),
                QueryRequestMethodPreference = QueryRequestMethodPreference.Auto,
                AutoPostQueryLengthThreshold = 100
            });

        var result = await client.GetLayerClient(0).QueryStatisticsAsync(
            new FeatureStatisticsQuery {
                Where = "1=1",
                GroupByFields = ["PLANNAME"],
                Statistics =
                [
                    new StatisticDefinition("OBJECTID", "FEATURE_COUNT", StatisticType.Count),
                    new StatisticDefinition("AOIID", "MAX_AOIID", StatisticType.Max)
                ]
            });

        Assert.Single(result);
        Assert.Equal(HttpMethod.Post, method);
        Assert.Equal("https://example.test/arcgis/rest/services/Test/FeatureServer/0/query", requestUri);
        Assert.NotNull(requestBody);
        Assert.Contains("outStatistics=", requestBody);
        Assert.Contains("groupByFieldsForStatistics=PLANNAME", requestBody);
    }
}