using System.Net.Http;
using S100Framework.REST.Clients;
using S100Framework.REST.Configuration;
using S100Framework.REST.Models;
using S100Framework.REST.Tests.TestDoubles;
using Xunit;

namespace S100Framework.REST.Tests.Clients;

public sealed class FeatureLayerClientTemporalQueryTests
{
    [Fact]
    public async Task QueryAsync_IncludesTimeInstantAndHistoricMoment_WhenPaginationIsSupported() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var timeInstant = new DateTimeOffset(2024, 01, 02, 03, 04, 05, TimeSpan.Zero);
        var historicMoment = new DateTimeOffset(2024, 01, 03, 04, 05, 06, TimeSpan.Zero);
        var requestUris = new List<string>();

        var client = CreateClient(request => {
            var uri = request.RequestUri!.AbsoluteUri;
            requestUris.Add(uri);

            if (IsLayerMetadataRequest(request)) {
                return CreateLayerMetadataResponse(supportsPagination: true);
            }

            if (uri.Contains("/FeatureServer/0/query?", StringComparison.OrdinalIgnoreCase)) {
                return StubHttpMessageHandler.Json("""
                {
                  "objectIdFieldName": "OBJECTID",
                  "features": [
                    {
                      "attributes": { "OBJECTID": 1, "NAME": "Feature 1" },
                      "geometry": { "x": 10, "y": 20 }
                    }
                  ]
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {uri}");
        });

        var layerClient = client.GetLayerClient(0);
        var results = new List<FeatureRecord>();

        await foreach (var feature in layerClient.QueryAsync(
            new FeatureQuery {
                TimeInstant = timeInstant,
                HistoricMoment = historicMoment
            },
            cancellationToken)) {
            results.Add(feature);
        }

        Assert.Single(results);

        var queryRequest = Assert.Single(
            requestUris,
            uri => uri.Contains("/FeatureServer/0/query?", StringComparison.OrdinalIgnoreCase));

        var decodedQueryRequest = Uri.UnescapeDataString(queryRequest);

        Assert.Contains(
            $"time={timeInstant.ToUnixTimeMilliseconds()}",
            decodedQueryRequest,
            StringComparison.Ordinal);

        Assert.Contains(
            $"historicMoment={historicMoment.ToUnixTimeMilliseconds()}",
            decodedQueryRequest,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task QueryCountAsync_IncludesOpenEndedTimeExtent_WhenProvided() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var end = new DateTimeOffset(2024, 01, 04, 05, 06, 07, TimeSpan.Zero);
        var requestUris = new List<string>();

        var client = CreateClient(request => {
            var uri = request.RequestUri!.AbsoluteUri;
            requestUris.Add(uri);

            if (uri.Contains("/FeatureServer/0/query?", StringComparison.OrdinalIgnoreCase)) {
                return StubHttpMessageHandler.Json("""
                {
                  "count": 7
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {uri}");
        });

        var layerClient = client.GetLayerClient(0);

        var count = await layerClient.QueryCountAsync(
            new FeatureQuery {
                TimeExtent = new FeatureTimeExtent(null, end)
            },
            cancellationToken);

        Assert.Equal(7, count);

        var queryRequest = Assert.Single(requestUris);
        var decodedQueryRequest = Uri.UnescapeDataString(queryRequest);

        Assert.Contains("returnCountOnly=true", decodedQueryRequest, StringComparison.Ordinal);
        Assert.Contains(
            $"time=null,{end.ToUnixTimeMilliseconds()}",
            decodedQueryRequest,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task QueryObjectIdsAsync_Throws_WhenTimeInstantAndTimeExtentAreBothSet() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = CreateClient(_ => throw new InvalidOperationException("HTTP should not be called."));
        var layerClient = client.GetLayerClient(0);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            layerClient.QueryObjectIdsAsync(
                new FeatureQuery {
                    TimeInstant = new DateTimeOffset(2024, 01, 02, 00, 00, 00, TimeSpan.Zero),
                    TimeExtent = new FeatureTimeExtent(
                        new DateTimeOffset(2024, 01, 01, 00, 00, 00, TimeSpan.Zero),
                        new DateTimeOffset(2024, 01, 03, 00, 00, 00, TimeSpan.Zero))
                },
                cancellationToken));

        Assert.Contains("TimeInstant", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("TimeExtent", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task QueryExtentAsync_Throws_WhenTimeExtentHasNoBounds() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = CreateClient(_ => throw new InvalidOperationException("HTTP should not be called."));
        var layerClient = client.GetLayerClient(0);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            layerClient.QueryExtentAsync(
                new FeatureQuery {
                    TimeExtent = new FeatureTimeExtent(null, null)
                },
                cancellationToken));

        Assert.Contains("TimeExtent", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task QueryObjectIdsAsync_Throws_WhenTimeExtentStartIsAfterEnd() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = CreateClient(_ => throw new InvalidOperationException("HTTP should not be called."));
        var layerClient = client.GetLayerClient(0);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            layerClient.QueryObjectIdsAsync(
                new FeatureQuery {
                    TimeExtent = new FeatureTimeExtent(
                        new DateTimeOffset(2024, 01, 03, 00, 00, 00, TimeSpan.Zero),
                        new DateTimeOffset(2024, 01, 01, 00, 00, 00, TimeSpan.Zero))
                },
                cancellationToken));

        Assert.Contains("TimeExtent.Start", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static FeatureServiceClient CreateClient(Func<HttpRequestMessage, HttpResponseMessage> handler) {
        return new FeatureServiceClient(
            new HttpClient(new StubHttpMessageHandler(handler)),
            new FeatureServiceClientOptions {
                ServiceUri = new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer")
            });
    }

    private static bool IsLayerMetadataRequest(HttpRequestMessage request) {
        return request.RequestUri?.AbsolutePath.EndsWith(
            "/FeatureServer/0",
            StringComparison.OrdinalIgnoreCase) == true;
    }

    private static HttpResponseMessage CreateLayerMetadataResponse(bool supportsPagination) {
        return StubHttpMessageHandler.Json($$"""
        {
          "id": 0,
          "name": "Layer 0",
          "geometryType": "esriGeometryPoint",
          "objectIdField": "OBJECTID",
          "maxRecordCount": 1000,
          "advancedQueryCapabilities": {
            "supportsPagination": {{supportsPagination.ToString().ToLowerInvariant()}}
          },
          "fields": [
            { "name": "OBJECTID", "type": "esriFieldTypeOID", "nullable": false },
            { "name": "NAME", "type": "esriFieldTypeString", "nullable": true }
          ],
          "relationships": [],
          "extent": {
            "spatialReference": { "wkid": 4326, "latestWkid": 4326 }
          }
        }
        """);
    }
}