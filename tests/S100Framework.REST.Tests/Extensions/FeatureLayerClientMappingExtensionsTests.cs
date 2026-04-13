using NetTopologySuite.Geometries;
using S100Framework.REST.Abstractions;
using S100Framework.REST.Clients;
using S100Framework.REST.Configuration;
using S100Framework.REST.Models;
using S100Framework.REST.Tests.TestDoubles;
using Xunit;

namespace S100Framework.REST.Tests.Clients;

public sealed class FeatureLayerClientQueryOptionsTests
{
    [Fact]
    public async Task QueryAsync_IncludesDistinctZMPAndGeometryOptions() {
        var requestUris = new List<string>();

        var handler = new StubHttpMessageHandler(request => {
            var uri = request.RequestUri!.AbsoluteUri;
            requestUris.Add(uri);

            if (uri.Contains("/FeatureServer/0?")) {
                return StubHttpMessageHandler.Json("""
                {
                  "id": 0,
                  "name": "DepthAreas",
                  "geometryType": "esriGeometryPoint",
                  "objectIdField": "OBJECTID",
                  "maxRecordCount": 1000,
                  "advancedQueryCapabilities": {
                    "supportsPagination": true
                  },
                  "fields": [
                    { "name": "OBJECTID", "type": "esriFieldTypeOID", "nullable": false }
                  ],
                  "extent": {
                    "spatialReference": { "wkid": 4326, "latestWkid": 4326 }
                  }
                }
                """);
            }

            if (uri.Contains("/FeatureServer/0/query?")) {
                return StubHttpMessageHandler.Json("""
                {
                  "objectIdFieldName": "OBJECTID",
                  "features": [
                    { "attributes": { "OBJECTID": 1 }, "geometry": { "x": 10, "y": 20, "z": 5 } }
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

        var query = new FeatureQuery {
            ReturnDistinctValues = true,
            ReturnZ = true,
            ReturnM = false,
            GeometryPrecision = 3,
            MaxAllowableOffset = 0.5,
            OutFields = ["OBJECTID"]
        };

        var results = new List<FeatureRecord>();

        await foreach (var feature in layerClient.QueryAsync(query)) {
            results.Add(feature);
        }

        Assert.Single(results);

        var queryRequest = Assert.Single(requestUris.Where(uri => uri.Contains("/FeatureServer/0/query?")));

        Assert.Contains("returnDistinctValues=true", queryRequest);
        Assert.Contains("returnZ=true", queryRequest);
        Assert.Contains("returnM=false", queryRequest);
        Assert.Contains("geometryPrecision=3", queryRequest);
        Assert.Contains("maxAllowableOffset=0.5", queryRequest);
    }

    [Fact]
    public async Task QueryCountAsync_ReturnsCount() {
        var handler = new StubHttpMessageHandler(request => {
            var uri = request.RequestUri!.AbsoluteUri;

            if (uri.Contains("/FeatureServer/0/query?")) {
                Assert.Contains("returnCountOnly=true", uri);

                return StubHttpMessageHandler.Json("""
                {
                  "count": 42
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

        var count = await layerClient.QueryCountAsync(new FeatureQuery());

        Assert.Equal(42, count);
    }

    [Fact]
    public async Task QueryObjectIdsAsync_ReturnsIds() {
        var handler = new StubHttpMessageHandler(request => {
            var uri = request.RequestUri!.AbsoluteUri;

            if (uri.Contains("/FeatureServer/0/query?")) {
                Assert.Contains("returnIdsOnly=true", uri);

                return StubHttpMessageHandler.Json("""
                {
                  "objectIdFieldName": "OBJECTID",
                  "objectIds": [1, 2, 3]
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

        var ids = await layerClient.QueryObjectIdsAsync(new FeatureQuery());

        Assert.Equal(new long[] { 1, 2, 3 }, ids.ToArray());
    }

    [Fact]
    public async Task QueryExtentAsync_ReturnsExtentAndSrid() {
        var handler = new StubHttpMessageHandler(request => {
            var uri = request.RequestUri!.AbsoluteUri;

            if (uri.Contains("/FeatureServer/0/query?")) {
                Assert.Contains("returnExtentOnly=true", uri);
                Assert.Contains("outSR=25832", uri);

                return StubHttpMessageHandler.Json("""
                {
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

        var extent = await layerClient.QueryExtentAsync(new FeatureQuery { OutSrid = 25832 });

        Assert.NotNull(extent);
        Assert.Equal(10, extent!.Envelope.MinX);
        Assert.Equal(30, extent.Envelope.MaxX);
        Assert.Equal(20, extent.Envelope.MinY);
        Assert.Equal(40, extent.Envelope.MaxY);
        Assert.Equal(25832, extent.Srid);
    }
}