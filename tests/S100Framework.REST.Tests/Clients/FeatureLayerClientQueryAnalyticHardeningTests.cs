using S100Framework.REST.Clients;
using S100Framework.REST.Configuration;
using S100Framework.REST.Exceptions;
using S100Framework.REST.Models;
using S100Framework.REST.Tests.TestDoubles;
using Xunit;

namespace S100Framework.REST.Tests.Clients;

public sealed class FeatureLayerClientQueryAnalyticHardeningTests
{
    [Fact]
    public async Task QueryAnalyticAsync_Throws_WhenLayerDoesNotAdvertiseQueryAnalyticSupport() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var queryAnalyticWasCalled = false;

        var client = CreateClient(request => {
            if (IsLayerMetadataRequest(request)) {
                return CreateLayerMetadataResponse(
                    supportsQueryAnalytic: false,
                    supportsAsyncQueryAnalytic: false);
            }

            if (IsQueryAnalyticRequest(request)) {
                queryAnalyticWasCalled = true;
            }

            throw new InvalidOperationException("HTTP should not be called after schema lookup.");
        });

        var exception = await Assert.ThrowsAsync<FeatureServiceCapabilityException>(() =>
            client.GetLayerClient(0).QueryAnalyticAsync(
                CreateRequest(),
                cancellationToken));

        Assert.Contains("queryAnalytic", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(queryAnalyticWasCalled);
    }

    [Fact]
    public async Task SubmitQueryAnalyticAsync_Throws_WhenLayerDoesNotAdvertiseQueryAnalyticSupport() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var queryAnalyticWasCalled = false;

        var client = CreateClient(request => {
            if (IsLayerMetadataRequest(request)) {
                return CreateLayerMetadataResponse(
                    supportsQueryAnalytic: false,
                    supportsAsyncQueryAnalytic: true);
            }

            if (IsQueryAnalyticRequest(request)) {
                queryAnalyticWasCalled = true;
            }

            throw new InvalidOperationException("HTTP should not be called after schema lookup.");
        });

        var exception = await Assert.ThrowsAsync<FeatureServiceCapabilityException>(() =>
            client.GetLayerClient(0).SubmitQueryAnalyticAsync(
                CreateRequest(),
                cancellationToken));

        Assert.Contains("queryAnalytic", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(queryAnalyticWasCalled);
    }

    [Fact]
    public async Task QueryAnalyticAsync_ReturnsEmptyRows_WhenFeaturesPropertyIsMissing() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsLayerMetadataRequest(request)) {
                return CreateLayerMetadataResponse(
                    supportsQueryAnalytic: true,
                    supportsAsyncQueryAnalytic: false);
            }

            if (IsQueryAnalyticRequest(request)) {
                return StubHttpMessageHandler.Json("""
                {
                  "objectIdFieldName": "OBJECTID"
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var result = await client.GetLayerClient(0).QueryAnalyticAsync(
            CreateRequest(),
            cancellationToken);

        Assert.Empty(result.Rows);
        Assert.Null(result.ExceededTransferLimit);
    }

    [Fact]
    public async Task QueryAnalyticAsync_ReturnsEmptyRows_WhenFeaturesPropertyIsNull() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsLayerMetadataRequest(request)) {
                return CreateLayerMetadataResponse(
                    supportsQueryAnalytic: true,
                    supportsAsyncQueryAnalytic: false);
            }

            if (IsQueryAnalyticRequest(request)) {
                return StubHttpMessageHandler.Json("""
                {
                  "features": null
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var result = await client.GetLayerClient(0).QueryAnalyticAsync(
            CreateRequest(),
            cancellationToken);

        Assert.Empty(result.Rows);
    }

    [Fact]
    public async Task QueryAnalyticAsync_IgnoresNullFeatureItems() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsLayerMetadataRequest(request)) {
                return CreateLayerMetadataResponse(
                    supportsQueryAnalytic: true,
                    supportsAsyncQueryAnalytic: false);
            }

            if (IsQueryAnalyticRequest(request)) {
                return StubHttpMessageHandler.Json("""
                {
                  "features": [
                    null,
                    {
                      "attributes": {
                        "OBJECTID": 10,
                        "STATE_NAME": "Texas",
                        "RunningTotal": 1234.5
                      }
                    }
                  ]
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var result = await client.GetLayerClient(0).QueryAnalyticAsync(
            CreateRequest(),
            cancellationToken);

        var row = Assert.Single(result.Rows);

        Assert.Equal(10, row.ObjectId);
        Assert.Equal("Texas", row.Attributes["STATE_NAME"]);
        Assert.Equal(1234.5m, row.Attributes["RunningTotal"]);
    }

    [Fact]
    public async Task QueryAnalyticAsync_Throws_WhenResultTypeIsInvalid() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(_ =>
            throw new InvalidOperationException("HTTP should not be called."));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.GetLayerClient(0).QueryAnalyticAsync(
                CreateRequest() with {
                    ResultType = (FeatureQueryResultType)999
                },
                cancellationToken));

        Assert.Contains("ResultType", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task QueryAnalyticAsync_Throws_WhenSqlFormatIsInvalid() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(_ =>
            throw new InvalidOperationException("HTTP should not be called."));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.GetLayerClient(0).QueryAnalyticAsync(
                CreateRequest() with {
                    SqlFormat = (FeatureQuerySqlFormat)999
                },
                cancellationToken));

        Assert.Contains("SqlFormat", exception.Message, StringComparison.Ordinal);
    }

    private static QueryAnalyticRequest CreateRequest() {
        return new QueryAnalyticRequest {
            Where = "POP1990 > 0",
            OutAnalyticsJson = """
            [
              {
                "analyticType": "SUM",
                "onAnalyticField": "POP1990",
                "outAnalyticFieldName": "RunningTotal",
                "analyticParameters": {
                  "partitionBy": "STATE_NAME",
                  "orderBy": "POP1990 ASC"
                }
              }
            ]
            """,
            OutFields = ["OBJECTID", "STATE_NAME"],
            ReturnGeometry = false
        };
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

    private static bool IsQueryAnalyticRequest(HttpRequestMessage request) {
        return request.RequestUri?.AbsolutePath.EndsWith(
            "/FeatureServer/0/queryAnalytic",
            StringComparison.OrdinalIgnoreCase) == true;
    }

    private static HttpResponseMessage CreateLayerMetadataResponse(
        bool supportsQueryAnalytic,
        bool supportsAsyncQueryAnalytic) {
        return StubHttpMessageHandler.Json($$"""
        {
          "id": 0,
          "name": "Layer 0",
          "geometryType": "esriGeometryPoint",
          "objectIdField": "OBJECTID",
          "maxRecordCount": 1000,
          "advancedQueryCapabilities": {
            "supportsPagination": true,
            "supportsQueryAnalytic": {{supportsQueryAnalytic.ToString().ToLowerInvariant()}}
          },
          "advancedQueryAnalyticCapabilities": {
            "supportsAsync": {{supportsAsyncQueryAnalytic.ToString().ToLowerInvariant()}}
          },
          "fields": [
            { "name": "OBJECTID", "type": "esriFieldTypeOID", "nullable": false },
            { "name": "STATE_NAME", "type": "esriFieldTypeString", "nullable": true },
            { "name": "POP1990", "type": "esriFieldTypeInteger", "nullable": true }
          ],
          "relationships": [],
          "extent": {
            "spatialReference": { "wkid": 4326, "latestWkid": 4326 }
          }
        }
        """);
    }
}