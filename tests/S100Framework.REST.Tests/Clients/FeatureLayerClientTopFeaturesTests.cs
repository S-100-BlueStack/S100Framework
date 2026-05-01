using NetTopologySuite.Geometries;
using S100Framework.REST.Abstractions;
using S100Framework.REST.Clients;
using S100Framework.REST.Configuration;
using S100Framework.REST.Exceptions;
using S100Framework.REST.Models;
using S100Framework.REST.Tests.TestDoubles;
using Xunit;

namespace S100Framework.REST.Tests.Clients;

public sealed class FeatureLayerClientTopFeaturesTests
{
    [Fact]
    public async Task QueryTopFeaturesAsync_SendsExpectedParameters_AndMapsFeatures() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var requestUris = new List<string>();

        var handler = new StubHttpMessageHandler(request => {
            var uri = request.RequestUri!.AbsoluteUri;
            requestUris.Add(uri);

            if (IsLayerMetadataRequest(request)) {
                return CreateLayerMetadataResponse(supportsTopFeaturesQuery: true);
            }

            if (IsTopFeaturesRequest(request)) {
                return StubHttpMessageHandler.Json("""
                {
                  "objectIdFieldName": "OBJECTID",
                  "features": [
                    {
                      "attributes": { "OBJECTID": 1, "PLANNAME": "Plan A" },
                      "geometry": { "x": 10, "y": 20 }
                    },
                    {
                      "attributes": { "OBJECTID": 2, "PLANNAME": "Plan B" },
                      "geometry": { "x": 11, "y": 21 }
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
            },
            cancellationToken);

        Assert.Equal(2, result.Count);
        Assert.Equal(1, result[0].ObjectId);
        Assert.Equal("Plan A", result[0].GetRequiredString("PLANNAME"));
        Assert.NotNull(result[0].Geometry);

        var requestUri = Assert.Single(requestUris, uri => uri.Contains("/queryTopFeatures?", StringComparison.OrdinalIgnoreCase));
        var decoded = Uri.UnescapeDataString(requestUri);

        Assert.Contains("outFields=OBJECTID,PLANNAME", decoded, StringComparison.Ordinal);
        Assert.Contains("returnGeometry=true", decoded, StringComparison.Ordinal);
        Assert.Contains("returnZ=false", decoded, StringComparison.Ordinal);
        Assert.Contains("returnM=false", decoded, StringComparison.Ordinal);
        Assert.Contains("outSR=4326", decoded, StringComparison.Ordinal);
        Assert.Contains("geometryPrecision=3", decoded, StringComparison.Ordinal);
        Assert.Contains("maxAllowableOffset=0.5", decoded, StringComparison.Ordinal);
        Assert.Contains("topFilter=", decoded, StringComparison.Ordinal);
        Assert.Contains("\"groupByFields\":\"REGION\"", decoded, StringComparison.Ordinal);
        Assert.Contains("\"topCount\":2", decoded, StringComparison.Ordinal);
        Assert.Contains("\"orderByFields\":\"SCORE DESC\"", decoded, StringComparison.Ordinal);
    }

    [Fact]
    public async Task QueryTopFeatureObjectIdsAsync_ReturnsIds() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var requestUris = new List<string>();

        var handler = new StubHttpMessageHandler(request => {
            var uri = request.RequestUri!.AbsoluteUri;
            requestUris.Add(uri);

            if (IsLayerMetadataRequest(request)) {
                return CreateLayerMetadataResponse(supportsTopFeaturesQuery: true);
            }

            if (IsTopFeaturesRequest(request)) {
                Assert.Contains("returnIdsOnly=true", uri, StringComparison.Ordinal);

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
            },
            cancellationToken);

        Assert.Equal(new long[] { 5, 7, 9 }, ids.ToArray());
        Assert.Contains(requestUris, uri => uri.EndsWith("/FeatureServer/0?f=json", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(requestUris, uri => uri.Contains("/queryTopFeatures?", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task QueryTopFeatureCountAsync_ReturnsCountAndExtent() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var requestUris = new List<string>();

        var handler = new StubHttpMessageHandler(request => {
            var uri = request.RequestUri!.AbsoluteUri;
            requestUris.Add(uri);

            if (IsLayerMetadataRequest(request)) {
                return CreateLayerMetadataResponse(supportsTopFeaturesQuery: true);
            }

            if (IsTopFeaturesRequest(request)) {
                Assert.Contains("returnCountOnly=true", uri, StringComparison.Ordinal);
                Assert.Contains("outSR=25832", uri, StringComparison.Ordinal);

                return StubHttpMessageHandler.Json("""
                {
                  "count": 4,
                  "extent": {
                    "xmin": 10,
                    "ymin": 20,
                    "xmax": 30,
                    "ymax": 40,
                    "spatialReference": { "wkid": 25832, "latestWkid": 25832 }
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
            },
            cancellationToken);

        Assert.Equal(4, result.Count);
        Assert.NotNull(result.Extent);
        Assert.Equal(10, result.Extent!.Envelope.MinX);
        Assert.Equal(30, result.Extent.Envelope.MaxX);
        Assert.Equal(20, result.Extent.Envelope.MinY);
        Assert.Equal(40, result.Extent.Envelope.MaxY);
        Assert.Equal(25832, result.Extent.Srid);
        Assert.Contains(requestUris, uri => uri.EndsWith("/FeatureServer/0?f=json", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(requestUris, uri => uri.Contains("/queryTopFeatures?", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task QueryTopFeatureObjectIdsAsync_Throws_WhenObjectIdsAreSpecified() {
        var cancellationToken = TestContext.Current.CancellationToken;

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
                },
                cancellationToken));

        Assert.Contains("returnIdsOnly", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task QueryTopFeaturesAsync_Throws_WhenLayerDoesNotSupportTopFeaturesQuery() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var requestUris = new List<string>();

        var layerClient = CreateLayerClient(request => {
            var uri = request.RequestUri!.AbsoluteUri;
            requestUris.Add(uri);

            if (IsLayerMetadataRequest(request)) {
                return CreateLayerMetadataResponse(supportsTopFeaturesQuery: false);
            }

            throw new InvalidOperationException("Top-features query should not be executed.");
        });

        var exception = await Assert.ThrowsAsync<FeatureServiceCapabilityException>(() =>
            layerClient.QueryTopFeaturesAsync(
                CreateValidTopFeaturesQuery(),
                cancellationToken));

        Assert.Contains("top-features", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Single(requestUris);
    }

    [Fact]
    public async Task QueryTopFeatureObjectIdsAsync_Throws_WhenLayerDoesNotSupportTopFeaturesQuery() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var requestUris = new List<string>();

        var layerClient = CreateLayerClient(request => {
            var uri = request.RequestUri!.AbsoluteUri;
            requestUris.Add(uri);

            if (IsLayerMetadataRequest(request)) {
                return CreateLayerMetadataResponse(supportsTopFeaturesQuery: false);
            }

            throw new InvalidOperationException("Top-features object ID query should not be executed.");
        });

        var exception = await Assert.ThrowsAsync<FeatureServiceCapabilityException>(() =>
            layerClient.QueryTopFeatureObjectIdsAsync(
                CreateValidTopFeaturesQuery(),
                cancellationToken));

        Assert.Contains("top-features", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Single(requestUris);
    }

    [Fact]
    public async Task QueryTopFeatureCountAsync_Throws_WhenLayerDoesNotSupportTopFeaturesQuery() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var requestUris = new List<string>();

        var layerClient = CreateLayerClient(request => {
            var uri = request.RequestUri!.AbsoluteUri;
            requestUris.Add(uri);

            if (IsLayerMetadataRequest(request)) {
                return CreateLayerMetadataResponse(supportsTopFeaturesQuery: false);
            }

            throw new InvalidOperationException("Top-features count query should not be executed.");
        });

        var exception = await Assert.ThrowsAsync<FeatureServiceCapabilityException>(() =>
            layerClient.QueryTopFeatureCountAsync(
                CreateValidTopFeaturesQuery(),
                cancellationToken));

        Assert.Contains("top-features", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Single(requestUris);
    }

    [Fact]
    public async Task QueryTopFeaturesAsync_SendsDefaultWhereObjectIdsSpatialFilterAndTopFilterJson() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var requestUris = new List<string>();

        var layerClient = CreateLayerClient(request => {
            var uri = request.RequestUri!.AbsoluteUri;
            requestUris.Add(uri);

            if (IsLayerMetadataRequest(request)) {
                return CreateLayerMetadataResponse(supportsTopFeaturesQuery: true);
            }

            if (IsTopFeaturesRequest(request)) {
                return StubHttpMessageHandler.Json("""
                {
                  "objectIdFieldName": "OBJECTID",
                  "features": []
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {uri}");
        });

        var result = await layerClient.QueryTopFeaturesAsync(
            new TopFeaturesQuery {
                Where = "   ",
                ObjectIds = [10, 20],
                SpatialFilter = FeatureSpatialFilter.FromEnvelope(
                    new Envelope(9, 12, 54, 57),
                    4326),
                TopFilter = new TopFeaturesFilter {
                    GroupByFields = ["REGION", "TYPE"],
                    OrderByFields = ["SCORE DESC", "OBJECTID ASC"],
                    TopCount = 3
                }
            },
            cancellationToken);

        Assert.Empty(result);

        var requestUri = Assert.Single(
            requestUris,
            uri => uri.Contains("/queryTopFeatures?", StringComparison.OrdinalIgnoreCase));

        var decoded = Uri.UnescapeDataString(requestUri);

        Assert.Contains("where=1=1", decoded, StringComparison.Ordinal);
        Assert.Contains("objectIds=10,20", decoded, StringComparison.Ordinal);
        Assert.Contains("geometryType=esriGeometryEnvelope", decoded, StringComparison.Ordinal);
        Assert.Contains("spatialRel=esriSpatialRelIntersects", decoded, StringComparison.Ordinal);
        Assert.Contains("inSR=4326", decoded, StringComparison.Ordinal);
        Assert.Contains("geometry=", decoded, StringComparison.Ordinal);
        Assert.Contains("topFilter=", decoded, StringComparison.Ordinal);
        Assert.Contains("\"groupByFields\":\"REGION,TYPE\"", decoded, StringComparison.Ordinal);
        Assert.Contains("\"orderByFields\":\"SCORE DESC,OBJECTID ASC\"", decoded, StringComparison.Ordinal);
        Assert.Contains("\"topCount\":3", decoded, StringComparison.Ordinal);
    }

    [Fact]
    public async Task QueryTopFeatureObjectIdsAsync_DoesNotSendFeatureSetOnlyParameters() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var requestUris = new List<string>();

        var layerClient = CreateLayerClient(request => {
            var uri = request.RequestUri!.AbsoluteUri;
            requestUris.Add(uri);

            if (IsLayerMetadataRequest(request)) {
                return CreateLayerMetadataResponse(supportsTopFeaturesQuery: true);
            }

            if (IsTopFeaturesRequest(request)) {
                return StubHttpMessageHandler.Json("""
                {
                  "objectIdFieldName": "OBJECTID",
                  "objectIds": [10, 20]
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {uri}");
        });

        var objectIds = await layerClient.QueryTopFeatureObjectIdsAsync(
            new TopFeaturesQuery {
                Where = "STATUS = 'Active'",
                OutFields = ["OBJECTID", "PLANNAME"],
                ReturnGeometry = true,
                ReturnZ = true,
                ReturnM = true,
                OutSrid = 25832,
                GeometryPrecision = 3,
                MaxAllowableOffset = 0.5,
                SpatialFilter = FeatureSpatialFilter.FromEnvelope(
                    new Envelope(9, 12, 54, 57),
                    4326),
                TopFilter = new TopFeaturesFilter {
                    GroupByFields = ["REGION"],
                    OrderByFields = ["SCORE DESC"],
                    TopCount = 2
                }
            },
            cancellationToken);

        Assert.Equal([10, 20], objectIds);

        var requestUri = Assert.Single(
            requestUris,
            uri => uri.Contains("/queryTopFeatures?", StringComparison.OrdinalIgnoreCase));

        var decoded = Uri.UnescapeDataString(requestUri);

        Assert.Contains("returnIdsOnly=true", decoded, StringComparison.Ordinal);
        Assert.Contains("where=STATUS = 'Active'", decoded, StringComparison.Ordinal);
        Assert.Contains("geometryType=esriGeometryEnvelope", decoded, StringComparison.Ordinal);
        Assert.Contains("inSR=4326", decoded, StringComparison.Ordinal);
        Assert.Contains("topFilter=", decoded, StringComparison.Ordinal);

        Assert.DoesNotContain("outFields=", decoded, StringComparison.Ordinal);
        Assert.DoesNotContain("returnGeometry=", decoded, StringComparison.Ordinal);
        Assert.DoesNotContain("returnZ=", decoded, StringComparison.Ordinal);
        Assert.DoesNotContain("returnM=", decoded, StringComparison.Ordinal);
        Assert.DoesNotContain("outSR=", decoded, StringComparison.Ordinal);
        Assert.DoesNotContain("geometryPrecision=", decoded, StringComparison.Ordinal);
        Assert.DoesNotContain("maxAllowableOffset=", decoded, StringComparison.Ordinal);
        Assert.DoesNotContain("returnCountOnly=", decoded, StringComparison.Ordinal);
    }

    [Fact]
    public async Task QueryTopFeatureCountAsync_DoesNotSendFeatureSetOnlyGeometryParameters() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var requestUris = new List<string>();

        var layerClient = CreateLayerClient(request => {
            var uri = request.RequestUri!.AbsoluteUri;
            requestUris.Add(uri);

            if (IsLayerMetadataRequest(request)) {
                return CreateLayerMetadataResponse(supportsTopFeaturesQuery: true);
            }

            if (IsTopFeaturesRequest(request)) {
                return StubHttpMessageHandler.Json("""
                {
                  "count": 5
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {uri}");
        });

        var result = await layerClient.QueryTopFeatureCountAsync(
            new TopFeaturesQuery {
                Where = "STATUS = 'Active'",
                OutFields = ["OBJECTID", "PLANNAME"],
                ReturnGeometry = true,
                ReturnZ = true,
                ReturnM = true,
                OutSrid = 25832,
                GeometryPrecision = 3,
                MaxAllowableOffset = 0.5,
                SpatialFilter = FeatureSpatialFilter.FromEnvelope(
                    new Envelope(9, 12, 54, 57),
                    4326),
                TopFilter = new TopFeaturesFilter {
                    GroupByFields = ["REGION"],
                    OrderByFields = ["SCORE DESC"],
                    TopCount = 2
                }
            },
            cancellationToken);

        Assert.Equal(5, result.Count);
        Assert.Null(result.Extent);

        var requestUri = Assert.Single(
            requestUris,
            uri => uri.Contains("/queryTopFeatures?", StringComparison.OrdinalIgnoreCase));

        var decoded = Uri.UnescapeDataString(requestUri);

        Assert.Contains("returnCountOnly=true", decoded, StringComparison.Ordinal);
        Assert.Contains("where=STATUS = 'Active'", decoded, StringComparison.Ordinal);
        Assert.Contains("outSR=25832", decoded, StringComparison.Ordinal);
        Assert.Contains("geometryType=esriGeometryEnvelope", decoded, StringComparison.Ordinal);
        Assert.Contains("inSR=4326", decoded, StringComparison.Ordinal);
        Assert.Contains("topFilter=", decoded, StringComparison.Ordinal);

        Assert.DoesNotContain("outFields=", decoded, StringComparison.Ordinal);
        Assert.DoesNotContain("returnGeometry=", decoded, StringComparison.Ordinal);
        Assert.DoesNotContain("returnZ=", decoded, StringComparison.Ordinal);
        Assert.DoesNotContain("returnM=", decoded, StringComparison.Ordinal);
        Assert.DoesNotContain("geometryPrecision=", decoded, StringComparison.Ordinal);
        Assert.DoesNotContain("maxAllowableOffset=", decoded, StringComparison.Ordinal);
        Assert.DoesNotContain("returnIdsOnly=", decoded, StringComparison.Ordinal);
    }

    [Fact]
    public async Task QueryTopFeaturesAsync_ThrowsBeforeHttp_WhenTopFilterIsMissing() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var layerClient = CreateLayerClient(_ =>
            throw new InvalidOperationException("HTTP should not be called."));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            layerClient.QueryTopFeaturesAsync(
                new TopFeaturesQuery(),
                cancellationToken));

        Assert.Contains("TopFilter", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task QueryTopFeaturesAsync_ThrowsBeforeHttp_WhenTopFilterGroupByFieldsAreMissing() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var layerClient = CreateLayerClient(_ =>
            throw new InvalidOperationException("HTTP should not be called."));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            layerClient.QueryTopFeaturesAsync(
                new TopFeaturesQuery {
                    TopFilter = new TopFeaturesFilter {
                        OrderByFields = ["SCORE DESC"],
                        TopCount = 1
                    }
                },
                cancellationToken));

        Assert.Contains("group-by", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task QueryTopFeaturesAsync_ThrowsBeforeHttp_WhenTopFilterOrderByFieldsAreMissing() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var layerClient = CreateLayerClient(_ =>
            throw new InvalidOperationException("HTTP should not be called."));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            layerClient.QueryTopFeaturesAsync(
                new TopFeaturesQuery {
                    TopFilter = new TopFeaturesFilter {
                        GroupByFields = ["REGION"],
                        TopCount = 1
                    }
                },
                cancellationToken));

        Assert.Contains("order-by", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task QueryTopFeaturesAsync_ThrowsBeforeHttp_WhenTopCountIsNotPositive() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var layerClient = CreateLayerClient(_ =>
            throw new InvalidOperationException("HTTP should not be called."));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            layerClient.QueryTopFeaturesAsync(
                new TopFeaturesQuery {
                    TopFilter = new TopFeaturesFilter {
                        GroupByFields = ["REGION"],
                        OrderByFields = ["SCORE DESC"],
                        TopCount = 0
                    }
                },
                cancellationToken));

        Assert.Contains("TopCount", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task QueryTopFeaturesAsync_IncludesSpatialDistanceAndUnits_WhenProvided() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var requestUris = new List<string>();

        var layerClient = CreateLayerClient(request => {
            var uri = request.RequestUri!.AbsoluteUri;
            requestUris.Add(uri);

            if (IsLayerMetadataRequest(request)) {
                return CreateLayerMetadataResponse(supportsTopFeaturesQuery: true);
            }

            if (IsTopFeaturesRequest(request)) {
                return StubHttpMessageHandler.Json("""
                {
                  "objectIdFieldName": "OBJECTID",
                  "features": []
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {uri}");
        });

        var result = await layerClient.QueryTopFeaturesAsync(
            new TopFeaturesQuery {
                SpatialFilter = FeatureSpatialFilter.FromGeometry(
                    new Point(12.34, 56.78) {
                        SRID = 4326
                    },
                    distance: 100,
                    distanceUnit: FeatureSpatialDistanceUnit.Meter),
                TopFilter = new TopFeaturesFilter {
                    GroupByFields = ["REGION"],
                    OrderByFields = ["SCORE DESC"],
                    TopCount = 2
                }
            },
            cancellationToken);

        Assert.Empty(result);

        var requestUri = Assert.Single(
            requestUris,
            uri => uri.Contains("/queryTopFeatures?", StringComparison.OrdinalIgnoreCase));

        var decoded = Uri.UnescapeDataString(requestUri);

        Assert.Contains("geometryType=esriGeometryPoint", decoded, StringComparison.Ordinal);
        Assert.Contains("spatialRel=esriSpatialRelIntersects", decoded, StringComparison.Ordinal);
        Assert.Contains("inSR=4326", decoded, StringComparison.Ordinal);
        Assert.Contains("distance=100", decoded, StringComparison.Ordinal);
        Assert.Contains("units=esriSRUnit_Meter", decoded, StringComparison.Ordinal);
        Assert.Contains("geometry=", decoded, StringComparison.Ordinal);
    }

    [Fact]
    public async Task QueryTopFeatureCountAsync_IncludesSpatialDistanceWithoutUnits_WhenUnitIsOmitted() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var requestUris = new List<string>();

        var layerClient = CreateLayerClient(request => {
            var uri = request.RequestUri!.AbsoluteUri;
            requestUris.Add(uri);

            if (IsLayerMetadataRequest(request)) {
                return CreateLayerMetadataResponse(supportsTopFeaturesQuery: true);
            }

            if (IsTopFeaturesRequest(request)) {
                return StubHttpMessageHandler.Json("""
                {
                  "count": 4
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {uri}");
        });

        var result = await layerClient.QueryTopFeatureCountAsync(
            new TopFeaturesQuery {
                SpatialFilter = FeatureSpatialFilter.FromEnvelope(
                    new Envelope(10, 11, 55, 56),
                    inSrid: 4326,
                    distance: 25),
                TopFilter = new TopFeaturesFilter {
                    GroupByFields = ["REGION"],
                    OrderByFields = ["SCORE DESC"],
                    TopCount = 2
                }
            },
            cancellationToken);

        Assert.Equal(4, result.Count);

        var requestUri = Assert.Single(
            requestUris,
            uri => uri.Contains("/queryTopFeatures?", StringComparison.OrdinalIgnoreCase));

        var decoded = Uri.UnescapeDataString(requestUri);

        Assert.Contains("returnCountOnly=true", decoded, StringComparison.Ordinal);
        Assert.Contains("geometryType=esriGeometryEnvelope", decoded, StringComparison.Ordinal);
        Assert.Contains("inSR=4326", decoded, StringComparison.Ordinal);
        Assert.Contains("distance=25", decoded, StringComparison.Ordinal);
        Assert.DoesNotContain("units=", decoded, StringComparison.Ordinal);
    }

    private static IFeatureLayerClient CreateLayerClient(Func<HttpRequestMessage, HttpResponseMessage> handler) {
        return new FeatureServiceClient(
            new HttpClient(new StubHttpMessageHandler(handler)),
            new FeatureServiceClientOptions {
                ServiceUri = new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer")
            }).GetLayerClient(0);
    }

    private static TopFeaturesQuery CreateValidTopFeaturesQuery() {
        return new TopFeaturesQuery {
            TopFilter = new TopFeaturesFilter {
                GroupByFields = ["REGION"],
                OrderByFields = ["SCORE DESC"],
                TopCount = 1
            }
        };
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

    private static HttpResponseMessage CreateLayerMetadataResponse(bool supportsTopFeaturesQuery) {
        return StubHttpMessageHandler.Json($$"""
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
          "supportsTopFeaturesQuery": {{supportsTopFeaturesQuery.ToString().ToLowerInvariant()}}
        }
        """);
    }
}