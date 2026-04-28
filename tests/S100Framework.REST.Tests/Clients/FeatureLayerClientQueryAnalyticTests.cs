using S100Framework.REST.Clients;
using S100Framework.REST.Configuration;
using S100Framework.REST.Models;
using S100Framework.REST.Tests.TestDoubles;
using Xunit;

namespace S100Framework.REST.Tests.Clients;

public sealed class FeatureLayerClientQueryAnalyticTests
{
    [Fact]
    public async Task QueryAnalyticAsync_SendsAnalyticParameters() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var requestUris = new List<Uri>();

        var client = CreateClient(request => {
            requestUris.Add(request.RequestUri!);

            if (request.RequestUri!.AbsolutePath.EndsWith(
                "/FeatureServer/0",
                StringComparison.OrdinalIgnoreCase)) {
                return CreateLayerMetadataResponse();
            }

            if (request.RequestUri!.AbsolutePath.EndsWith(
                "/FeatureServer/0/queryAnalytic",
                StringComparison.OrdinalIgnoreCase)) {
                return StubHttpMessageHandler.Json("""
                {
                  "objectIdFieldName": "OBJECTID",
                  "exceededTransferLimit": false,
                  "features": [
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

        var layerClient = client.GetLayerClient(0);

        var result = await layerClient.QueryAnalyticAsync(
            new QueryAnalyticRequest {
                Where = "POP1990 > 0",
                AnalyticWhere = "RunningTotal > 100",
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
                ReturnGeometry = false,
                OutSrid = 4326,
                OrderBy = "STATE_NAME ASC",
                ResultType = FeatureQueryResultType.Standard,
                CacheHint = true,
                ResultOffset = 5,
                ResultRecordCount = 25,
                QuantizationParametersJson = """
                {
                  "mode": "view",
                  "originPosition": "upperLeft",
                  "tolerance": 1,
                  "extent": {
                    "xmin": 0,
                    "ymin": 0,
                    "xmax": 10,
                    "ymax": 10,
                    "spatialReference": { "wkid": 4326 }
                  }
                }
                """,
                SqlFormat = FeatureQuerySqlFormat.Standard
            },
            cancellationToken);

        Assert.False(result.ExceededTransferLimit);

        var row = Assert.Single(result.Rows);
        Assert.Null(row.Geometry);
        Assert.Equal(10, row.ObjectId);
        Assert.Equal("Texas", row.Attributes["STATE_NAME"]);
        Assert.Equal(1234.5m, row.Attributes["RunningTotal"]);

        var queryRequest = Assert.Single(
            requestUris,
            uri => uri.AbsolutePath.EndsWith(
                "/FeatureServer/0/queryAnalytic",
                StringComparison.OrdinalIgnoreCase));

        var query = ParseQuery(queryRequest);

        Assert.Equal("json", query["f"]);
        Assert.Equal("json", query["dataFormat"]);
        Assert.Equal("POP1990 > 0", query["where"]);
        Assert.Equal("RunningTotal > 100", query["analyticWhere"]);
        Assert.Equal("OBJECTID,STATE_NAME", query["outFields"]);
        Assert.Equal("false", query["returnGeometry"]);
        Assert.Equal("4326", query["outSR"]);
        Assert.Equal("STATE_NAME ASC", query["orderByFields"]);
        Assert.Equal("standard", query["resultType"]);
        Assert.Equal("true", query["cacheHint"]);
        Assert.Equal("5", query["resultOffset"]);
        Assert.Equal("25", query["resultRecordCount"]);
        Assert.Equal("standard", query["sqlFormat"]);
        Assert.Contains("\"analyticType\": \"SUM\"", query["outAnalytics"], StringComparison.Ordinal);
        Assert.Contains("\"mode\": \"view\"", query["quantizationParameters"], StringComparison.Ordinal);
    }

    [Fact]
    public async Task QueryAnalyticAsync_MapsReturnedGeometry() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (request.RequestUri!.AbsolutePath.EndsWith(
                "/FeatureServer/0",
                StringComparison.OrdinalIgnoreCase)) {
                return CreateLayerMetadataResponse();
            }

            if (request.RequestUri!.AbsolutePath.EndsWith(
                "/FeatureServer/0/queryAnalytic",
                StringComparison.OrdinalIgnoreCase)) {
                return StubHttpMessageHandler.Json("""
                {
                  "objectIdFieldName": "OBJECTID",
                  "features": [
                    {
                      "attributes": {
                        "OBJECTID": 11,
                        "STATE_NAME": "Oregon",
                        "RankValue": 1
                      },
                      "geometry": {
                        "x": -122.5,
                        "y": 45.5,
                        "spatialReference": { "wkid": 4326 }
                      }
                    }
                  ]
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var layerClient = client.GetLayerClient(0);

        var result = await layerClient.QueryAnalyticAsync(
            new QueryAnalyticRequest {
                OutAnalyticsJson = """
                [
                  {
                    "analyticType": "RANK",
                    "onAnalyticField": "POP1990",
                    "outAnalyticFieldName": "RankValue"
                  }
                ]
                """
            },
            cancellationToken);

        var row = Assert.Single(result.Rows);

        Assert.NotNull(row.Geometry);
        Assert.Equal("Point", row.Geometry!.GeometryType);
        Assert.Equal(11, row.ObjectId);
        Assert.Equal("Oregon", row.Attributes["STATE_NAME"]);
        Assert.Equal(1L, row.Attributes["RankValue"]);
    }

    [Fact]
    public async Task QueryAnalyticAsync_Throws_WhenOutAnalyticsJsonIsInvalid() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(_ =>
            throw new InvalidOperationException("HTTP should not be called."));

        var layerClient = client.GetLayerClient(0);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            layerClient.QueryAnalyticAsync(
                new QueryAnalyticRequest {
                    OutAnalyticsJson = "not-json"
                },
                cancellationToken));

        Assert.Contains("OutAnalyticsJson", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetSchemaAsync_MapsQueryAnalyticCapability() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (request.RequestUri!.AbsolutePath.EndsWith(
                "/FeatureServer/0",
                StringComparison.OrdinalIgnoreCase)) {
                return CreateLayerMetadataResponse();
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var schema = await client.GetLayerClient(0).GetSchemaAsync(cancellationToken);

        Assert.True(schema.Capabilities.SupportsQueryAnalytic);
    }

    private static FeatureServiceClient CreateClient(Func<HttpRequestMessage, HttpResponseMessage> handler) {
        return new FeatureServiceClient(
            new HttpClient(new StubHttpMessageHandler(handler)),
            new FeatureServiceClientOptions {
                ServiceUri = new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer")
            });
    }

    private static HttpResponseMessage CreateLayerMetadataResponse() {
        return StubHttpMessageHandler.Json("""
        {
          "id": 0,
          "name": "Layer 0",
          "geometryType": "esriGeometryPoint",
          "objectIdField": "OBJECTID",
          "maxRecordCount": 1000,
          "advancedQueryCapabilities": {
            "supportsPagination": true,
            "supportsQueryAnalytic": true
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

    private static Dictionary<string, string> ParseQuery(Uri uri) {
        return uri.Query
            .TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Split('=', 2))
            .ToDictionary(
                parts => Uri.UnescapeDataString(parts[0]),
                parts => parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : string.Empty,
                StringComparer.Ordinal);
    }
}