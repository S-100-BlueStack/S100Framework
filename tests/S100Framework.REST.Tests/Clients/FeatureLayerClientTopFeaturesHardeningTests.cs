using S100Framework.REST.Clients;
using S100Framework.REST.Configuration;
using S100Framework.REST.Models;
using S100Framework.REST.Tests.TestDoubles;
using Xunit;

namespace S100Framework.REST.Tests.Clients;

public sealed class FeatureLayerClientTopFeaturesHardeningTests
{
    [Fact]
    public async Task QueryTopFeaturesAsync_ReturnsEmptyList_WhenFeaturesPropertyIsMissing() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsLayerMetadataRequest(request)) {
                return CreateLayerMetadataResponse();
            }

            if (IsTopFeaturesRequest(request)) {
                return StubHttpMessageHandler.Json("{}");
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var result = await client.GetLayerClient(0).QueryTopFeaturesAsync(
            CreateQuery(),
            cancellationToken);

        Assert.Empty(result);
    }

    [Fact]
    public async Task QueryTopFeaturesAsync_ReturnsEmptyList_WhenFeaturesPropertyIsNull() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsLayerMetadataRequest(request)) {
                return CreateLayerMetadataResponse();
            }

            if (IsTopFeaturesRequest(request)) {
                return StubHttpMessageHandler.Json("""
                {
                  "features": null
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var result = await client.GetLayerClient(0).QueryTopFeaturesAsync(
            CreateQuery(),
            cancellationToken);

        Assert.Empty(result);
    }

    [Fact]
    public async Task QueryTopFeaturesAsync_IgnoresNullFeatureItems() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsLayerMetadataRequest(request)) {
                return CreateLayerMetadataResponse();
            }

            if (IsTopFeaturesRequest(request)) {
                return StubHttpMessageHandler.Json("""
                {
                  "objectIdFieldName": "OBJECTID",
                  "features": [
                    null,
                    {
                      "attributes": {
                        "OBJECTID": 10,
                        "PLANNAME": "Plan A"
                      },
                      "geometry": {
                        "x": 12.4,
                        "y": 55.7
                      }
                    }
                  ]
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var result = await client.GetLayerClient(0).QueryTopFeaturesAsync(
            CreateQuery(),
            cancellationToken);

        var record = Assert.Single(result);

        Assert.Equal(10, record.ObjectId);
        Assert.Equal("Plan A", record.Attributes["PLANNAME"]);
    }

    [Fact]
    public async Task QueryTopFeaturesAsync_Throws_WhenOutFieldsContainsBlankValue() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(_ =>
            throw new InvalidOperationException("HTTP should not be called."));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.GetLayerClient(0).QueryTopFeaturesAsync(
                CreateQuery() with {
                    OutFields = ["OBJECTID", " "]
                },
                cancellationToken));

        Assert.Contains("OutFields", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task QueryTopFeaturesAsync_Throws_WhenOutSridIsNotPositive() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(_ =>
            throw new InvalidOperationException("HTTP should not be called."));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.GetLayerClient(0).QueryTopFeaturesAsync(
                CreateQuery() with {
                    OutSrid = 0
                },
                cancellationToken));

        Assert.Contains("OutSrid", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task QueryTopFeaturesAsync_Throws_WhenGeometryPrecisionIsNegative() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(_ =>
            throw new InvalidOperationException("HTTP should not be called."));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.GetLayerClient(0).QueryTopFeaturesAsync(
                CreateQuery() with {
                    GeometryPrecision = -1
                },
                cancellationToken));

        Assert.Contains("GeometryPrecision", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task QueryTopFeaturesAsync_Throws_WhenMaxAllowableOffsetIsNegative() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(_ =>
            throw new InvalidOperationException("HTTP should not be called."));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.GetLayerClient(0).QueryTopFeaturesAsync(
                CreateQuery() with {
                    MaxAllowableOffset = -0.1
                },
                cancellationToken));

        Assert.Contains("MaxAllowableOffset", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task QueryTopFeaturesAsync_Throws_WhenGroupByFieldsContainsBlankValue() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(_ =>
            throw new InvalidOperationException("HTTP should not be called."));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.GetLayerClient(0).QueryTopFeaturesAsync(
                CreateQuery() with {
                    TopFilter = new TopFeaturesFilter {
                        GroupByFields = ["REGION", " "],
                        OrderByFields = ["SCORE DESC"],
                        TopCount = 1
                    }
                },
                cancellationToken));

        Assert.Contains("GroupByFields", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task QueryTopFeaturesAsync_Throws_WhenOrderByFieldsContainsBlankValue() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(_ =>
            throw new InvalidOperationException("HTTP should not be called."));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.GetLayerClient(0).QueryTopFeaturesAsync(
                CreateQuery() with {
                    TopFilter = new TopFeaturesFilter {
                        GroupByFields = ["REGION"],
                        OrderByFields = ["SCORE DESC", ""],
                        TopCount = 1
                    }
                },
                cancellationToken));

        Assert.Contains("OrderByFields", exception.Message, StringComparison.Ordinal);
    }

    private static TopFeaturesQuery CreateQuery() {
        return new TopFeaturesQuery {
            OutFields = ["OBJECTID", "PLANNAME"],
            ReturnGeometry = true,
            TopFilter = new TopFeaturesFilter {
                GroupByFields = ["REGION"],
                OrderByFields = ["SCORE DESC"],
                TopCount = 1
            }
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

    private static bool IsTopFeaturesRequest(HttpRequestMessage request) {
        return request.RequestUri?.AbsolutePath.EndsWith(
            "/FeatureServer/0/queryTopFeatures",
            StringComparison.OrdinalIgnoreCase) == true;
    }

    private static HttpResponseMessage CreateLayerMetadataResponse() {
        return StubHttpMessageHandler.Json("""
        {
          "id": 0,
          "name": "TopLayer",
          "geometryType": "esriGeometryPoint",
          "objectIdField": "OBJECTID",
          "maxRecordCount": 1000,
          "advancedQueryCapabilities": {
            "supportsPagination": true
          },
          "fields": [
            { "name": "OBJECTID", "type": "esriFieldTypeOID", "nullable": false },
            { "name": "PLANNAME", "type": "esriFieldTypeString", "nullable": true }
          ],
          "relationships": [],
          "extent": {
            "spatialReference": { "wkid": 4326, "latestWkid": 4326 }
          },
          "hasAttachments": false,
          "supportsQueryAttachments": false,
          "supportsAttachmentsResizing": false,
          "supportsTopFeaturesQuery": true
        }
        """);
    }
}