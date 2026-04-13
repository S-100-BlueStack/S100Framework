using NetTopologySuite.Geometries;
using S100Framework.REST.Abstractions;
using S100Framework.REST.Clients;
using S100Framework.REST.Configuration;
using S100Framework.REST.Models;
using S100Framework.REST.Tests.TestDoubles;
using Xunit;

namespace S100Framework.REST.Tests.Clients;

public sealed class FeatureLayerClientSpatialQueryTests
{
    [Fact]
    public async Task QueryAsync_IncludesEnvelopeFilterAndOutSrid_WhenPaginationIsSupported() {
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
                    { "attributes": { "OBJECTID": 1 }, "geometry": { "x": 10, "y": 20 } }
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
            OutSrid = 25832,
            SpatialFilter = FeatureSpatialFilter.FromEnvelope(
                new Envelope(10, 30, 20, 40),
                inSrid: 4326,
                spatialRelation: EsriSpatialRelationships.Intersects)
        };

        var results = new List<FeatureRecord>();

        await foreach (var feature in layerClient.QueryAsync(query)) {
            results.Add(feature);
        }

        Assert.Single(results);

        var queryRequest = Assert.Single(requestUris.Where(uri => uri.Contains("/FeatureServer/0/query?")));

        Assert.Contains("geometry=", queryRequest);
        Assert.Contains("geometryType=esriGeometryEnvelope", queryRequest);
        Assert.Contains("spatialRel=esriSpatialRelIntersects", queryRequest);
        Assert.Contains("inSR=4326", queryRequest);
        Assert.Contains("outSR=25832", queryRequest);
        Assert.Contains("xmin", Uri.UnescapeDataString(queryRequest));
        Assert.Contains("xmax", Uri.UnescapeDataString(queryRequest));
    }

    [Fact]
    public async Task QueryAsync_ForwardsEnvelopeFilterToIdQueryFallback_WhenPaginationIsNotSupported() {
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
                  "maxRecordCount": 2,
                  "advancedQueryCapabilities": {
                    "supportsPagination": false
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

            if (uri.Contains("returnIdsOnly=true")) {
                return StubHttpMessageHandler.Json("""
                {
                  "objectIdFieldName": "OBJECTID",
                  "objectIds": [1, 2]
                }
                """);
            }

            if (uri.Contains("objectIds=1%2C2")) {
                return StubHttpMessageHandler.Json("""
                {
                  "objectIdFieldName": "OBJECTID",
                  "features": [
                    { "attributes": { "OBJECTID": 1 }, "geometry": { "x": 10, "y": 20 } },
                    { "attributes": { "OBJECTID": 2 }, "geometry": { "x": 11, "y": 21 } }
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
            SpatialFilter = FeatureSpatialFilter.FromEnvelope(
                new Envelope(10, 30, 20, 40),
                inSrid: 4326,
                spatialRelation: EsriSpatialRelationships.Intersects)
        };

        var results = new List<FeatureRecord>();

        await foreach (var feature in layerClient.QueryAsync(query)) {
            results.Add(feature);
        }

        Assert.Equal(2, results.Count);

        var idsRequest = Assert.Single(requestUris.Where(uri => uri.Contains("returnIdsOnly=true")));

        Assert.Contains("geometry=", idsRequest);
        Assert.Contains("geometryType=esriGeometryEnvelope", idsRequest);
        Assert.Contains("spatialRel=esriSpatialRelIntersects", idsRequest);
        Assert.Contains("inSR=4326", idsRequest);
    }

    [Fact]
    public async Task QueryAsync_IncludesPolygonGeometryFilter_WhenUsingNtsGeometry() {
        var requestUris = new List<string>();

        var handler = new StubHttpMessageHandler(request => {
            var uri = request.RequestUri!.AbsoluteUri;
            requestUris.Add(uri);

            if (uri.Contains("/FeatureServer/0?")) {
                return StubHttpMessageHandler.Json("""
                {
                  "id": 0,
                  "name": "DepthAreas",
                  "geometryType": "esriGeometryPolygon",
                  "objectIdField": "OBJECTID",
                  "maxRecordCount": 1000,
                  "advancedQueryCapabilities": {
                    "supportsPagination": true
                  },
                  "fields": [
                    { "name": "OBJECTID", "type": "esriFieldTypeOID", "nullable": false }
                  ],
                  "extent": {
                    "spatialReference": { "wkid": 25832, "latestWkid": 25832 }
                  }
                }
                """);
            }

            if (uri.Contains("/FeatureServer/0/query?")) {
                return StubHttpMessageHandler.Json("""
                {
                  "objectIdFieldName": "OBJECTID",
                  "features": [
                    { "attributes": { "OBJECTID": 1 }, "geometry": { "x": 10, "y": 20 } }
                  ]
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {uri}");
        });

        var geometryFactory = NetTopologySuite.NtsGeometryServices.Instance.CreateGeometryFactory(srid: 25832);

        var polygon = geometryFactory.CreatePolygon(
            [
                new Coordinate(530000, 6150000),
                new Coordinate(540000, 6150000),
                new Coordinate(540000, 6160000),
                new Coordinate(530000, 6160000),
                new Coordinate(530000, 6150000)
            ]);

        IFeatureServiceClient serviceClient = new FeatureServiceClient(
            new HttpClient(handler),
            new FeatureServiceClientOptions {
                ServiceUri = new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer")
            });

        var layerClient = serviceClient.GetLayerClient(0);

        var query = new FeatureQuery {
            SpatialFilter = FeatureSpatialFilter.FromGeometry(
                polygon,
                spatialRelation: EsriSpatialRelationships.Intersects)
        };

        await foreach (var _ in layerClient.QueryAsync(query)) {
        }

        var queryRequest = Assert.Single(requestUris.Where(uri => uri.Contains("/FeatureServer/0/query?")));

        Assert.Contains("geometryType=esriGeometryPolygon", queryRequest);
        Assert.Contains("spatialRel=esriSpatialRelIntersects", queryRequest);
        Assert.Contains("inSR=25832", queryRequest);
        Assert.Contains("rings", Uri.UnescapeDataString(queryRequest));
    }
}