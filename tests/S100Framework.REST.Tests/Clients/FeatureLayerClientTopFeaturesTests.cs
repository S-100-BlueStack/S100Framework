using S100Framework.REST.Abstractions;
using S100Framework.REST.Clients;
using S100Framework.REST.Configuration;
using S100Framework.REST.Models;
using S100Framework.REST.Tests.TestDoubles;
using Xunit;

namespace S100Framework.REST.Tests.Clients;

public sealed class FeatureLayerClientTopFeaturesTests
{
    [Fact]
    public async Task QueryTopFeaturesAsync_SendsExpectedParameters_AndMapsFeatures() {
        var requestUris = new List<string>();

        var handler = new StubHttpMessageHandler(request => {
            var uri = request.RequestUri!.AbsoluteUri;
            requestUris.Add(uri);

            if (uri.Contains("/FeatureServer/0?")) {
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
                  "extent": {
                    "spatialReference": { "wkid": 4326, "latestWkid": 4326 }
                  }
                }
                """);
            }

            if (uri.Contains("/FeatureServer/0/queryTopFeatures?")) {
                return StubHttpMessageHandler.Json("""
                {
                  "objectIdFieldName": "OBJECTID",
                  "features": [
                    {
                      "attributes": {
                        "OBJECTID": 1,
                        "PLANNAME": "Plan A"
                      },
                      "geometry": {
                        "x": 10,
                        "y": 20
                      }
                    },
                    {
                      "attributes": {
                        "OBJECTID": 2,
                        "PLANNAME": "Plan B"
                      },
                      "geometry": {
                        "x": 11,
                        "y": 21
                      }
                    }
                  ]
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {uri}");
        });

        IFeatureServiceClient serviceClient = new FeatureServiceClient(
            new HttpClient(handler),
            new FeatureServiceClientOptions {
                ServiceUri = new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer")
            });

        var layerClient = serviceClient.GetLayerClient(0);

        var result = await layerClient.QueryTopFeaturesAsync(
            new TopFeaturesQuery {
                Where = "1=1",
                OutFields = ["OBJECTID", "PLANNAME"],
                ReturnGeometry = true,
                ReturnZ = false,
                ReturnM = false,
                OutSrid = 4326,
                GeometryPrecision = 3,
                MaxAllowableOffset = 0.5,
                TopFilter = new TopFeaturesFilter {
                    GroupByFields = ["REGION"],
                    OrderByFields = ["SCORE DESC"],
                    TopCount = 2
                }
            });

        Assert.Equal(2, result.Count);
        Assert.Equal(1, result[0].ObjectId);
        Assert.Equal("Plan A", result[0].GetRequiredString("PLANNAME"));
        Assert.NotNull(result[0].Geometry);

        var requestUri = Assert.Single(requestUris.Where(x => x.Contains("/queryTopFeatures?")));
        var decoded = Uri.UnescapeDataString(requestUri);

        Assert.Contains("outFields=OBJECTID,PLANNAME", decoded);
        Assert.Contains("returnGeometry=true", decoded);
        Assert.Contains("returnZ=false", decoded);
        Assert.Contains("returnM=false", decoded);
        Assert.Contains("outSR=4326", decoded);
        Assert.Contains("geometryPrecision=3", decoded);
        Assert.Contains("maxAllowableOffset=0.5", decoded);
        Assert.Contains("topFilter=", decoded);
        Assert.Contains("\"groupByFields\":\"REGION\"", decoded);
        Assert.Contains("\"topCount\":2", decoded);
        Assert.Contains("\"orderByFields\":\"SCORE DESC\"", decoded);
    }

    [Fact]
    public async Task QueryTopFeatureObjectIdsAsync_ReturnsIds() {
        var handler = new StubHttpMessageHandler(request => {
            var uri = request.RequestUri!.AbsoluteUri;

            if (uri.Contains("/FeatureServer/0/queryTopFeatures?")) {
                Assert.Contains("returnIdsOnly=true", uri);

                return StubHttpMessageHandler.Json("""
                {
                  "objectIdFieldName": "OBJECTID",
                  "objectIds": [5, 7, 9]
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {uri}");
        });

        var layerClient = new FeatureServiceClient(
            new HttpClient(handler),
            new FeatureServiceClientOptions {
                ServiceUri = new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer")
            }).GetLayerClient(0);

        var ids = await layerClient.QueryTopFeatureObjectIdsAsync(
            new TopFeaturesQuery {
                TopFilter = new TopFeaturesFilter {
                    GroupByFields = ["REGION"],
                    OrderByFields = ["SCORE DESC"],
                    TopCount = 1
                }
            });

        Assert.Equal(new long[] { 5, 7, 9 }, ids.ToArray());
    }

    [Fact]
    public async Task QueryTopFeatureCountAsync_ReturnsCountAndExtent() {
        var handler = new StubHttpMessageHandler(request => {
            var uri = request.RequestUri!.AbsoluteUri;

            if (uri.Contains("/FeatureServer/0/queryTopFeatures?")) {
                Assert.Contains("returnCountOnly=true", uri);
                Assert.Contains("outSR=25832", uri);

                return StubHttpMessageHandler.Json("""
                {
                  "count": 4,
                  "extent": {
                    "xmin": 10,
                    "ymin": 20,
                    "xmax": 30,
                    "ymax": 40,
                    "spatialReference": {
                      "wkid": 25832,
                      "latestWkid": 25832
                    }
                  }
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {uri}");
        });

        var layerClient = new FeatureServiceClient(
            new HttpClient(handler),
            new FeatureServiceClientOptions {
                ServiceUri = new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer")
            }).GetLayerClient(0);

        var result = await layerClient.QueryTopFeatureCountAsync(
            new TopFeaturesQuery {
                OutSrid = 25832,
                TopFilter = new TopFeaturesFilter {
                    GroupByFields = ["REGION"],
                    OrderByFields = ["SCORE DESC"],
                    TopCount = 2
                }
            });

        Assert.Equal(4, result.Count);
        Assert.NotNull(result.Extent);
        Assert.Equal(10, result.Extent!.Envelope.MinX);
        Assert.Equal(30, result.Extent.Envelope.MaxX);
        Assert.Equal(20, result.Extent.Envelope.MinY);
        Assert.Equal(40, result.Extent.Envelope.MaxY);
        Assert.Equal(25832, result.Extent.Srid);
    }

    [Fact]
    public async Task QueryTopFeatureObjectIdsAsync_Throws_WhenObjectIdsAreSpecified() {
        var layerClient = new FeatureServiceClient(
            new HttpClient(new StubHttpMessageHandler(_ =>
                throw new InvalidOperationException("The HTTP request should not be executed."))),
            new FeatureServiceClientOptions {
                ServiceUri = new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer")
            }).GetLayerClient(0);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            layerClient.QueryTopFeatureObjectIdsAsync(
                new TopFeaturesQuery {
                    ObjectIds = [1, 2],
                    TopFilter = new TopFeaturesFilter {
                        GroupByFields = ["REGION"],
                        OrderByFields = ["SCORE DESC"],
                        TopCount = 1
                    }
                }));

        Assert.Contains("returnIdsOnly", exception.Message);
    }
}