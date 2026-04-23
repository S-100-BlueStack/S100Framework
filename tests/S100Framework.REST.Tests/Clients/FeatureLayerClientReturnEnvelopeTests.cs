using System.Net.Http;
using NetTopologySuite.Geometries;
using S100Framework.REST.Clients;
using S100Framework.REST.Configuration;
using S100Framework.REST.Models;
using S100Framework.REST.Tests.TestDoubles;
using Xunit;

namespace S100Framework.REST.Tests.Clients;

public sealed class FeatureLayerClientReturnEnvelopeTests
{
    [Fact]
    public async Task GetSchemaAsync_SetsSupportsReturningGeometryEnvelope_WhenMetadataExposesCapability() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsLayerMetadataRequest(request)) {
                return CreateLayerMetadataResponse(
                    supportsPagination: true,
                    supportsReturningGeometryEnvelope: true);
            }

            throw new InvalidOperationException("Unexpected request.");
        });

        var schema = await client.GetLayerClient(0).GetSchemaAsync(cancellationToken);

        Assert.True(schema.Capabilities.SupportsReturningGeometryEnvelope);
    }

    [Fact]
    public async Task QueryAsync_IncludesReturnEnvelopeAndMapsEnvelopeGeometry_WhenSupported() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var requestUris = new List<string>();

        var client = CreateClient(request => {
            var uri = request.RequestUri!.AbsoluteUri;
            requestUris.Add(uri);

            if (IsLayerMetadataRequest(request)) {
                return CreateLayerMetadataResponse(
                    supportsPagination: true,
                    supportsReturningGeometryEnvelope: true);
            }

            if (uri.Contains("/FeatureServer/0/query?", StringComparison.OrdinalIgnoreCase)) {
                return StubHttpMessageHandler.Json("""
                {
                  "objectIdFieldName": "OBJECTID",
                  "features": [
                    {
                      "attributes": { "OBJECTID": 42, "NAME": "Feature 42" },
                      "geometry": {
                        "xmin": 10,
                        "ymin": 20,
                        "xmax": 30,
                        "ymax": 40,
                        "spatialReference": { "wkid": 4326, "latestWkid": 4326 }
                      }
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
                ReturnEnvelope = true
            },
            cancellationToken)) {
            results.Add(feature);
        }

        var record = Assert.Single(results);
        var geometry = Assert.IsType<Polygon>(record.Geometry);

        Assert.Equal(10d, geometry.EnvelopeInternal.MinX);
        Assert.Equal(20d, geometry.EnvelopeInternal.MinY);
        Assert.Equal(30d, geometry.EnvelopeInternal.MaxX);
        Assert.Equal(40d, geometry.EnvelopeInternal.MaxY);
        Assert.Equal(4326, geometry.SRID);

        var queryRequest = Assert.Single(
            requestUris,
            uri => uri.Contains("/FeatureServer/0/query?", StringComparison.OrdinalIgnoreCase));

        var decodedQueryRequest = Uri.UnescapeDataString(queryRequest);

        Assert.Contains("returnEnvelope=true", decodedQueryRequest, StringComparison.Ordinal);
    }

    [Fact]
    public async Task QueryAsync_Throws_WhenReturnEnvelopeIsRequestedWithoutReturnGeometry() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = CreateClient(_ => throw new InvalidOperationException("HTTP should not be called."));
        var layerClient = client.GetLayerClient(0);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () => {
            await foreach (var _ in layerClient.QueryAsync(
                new FeatureQuery {
                    ReturnGeometry = false,
                    ReturnEnvelope = true
                },
                cancellationToken)) {
            }
        });

        Assert.Contains("ReturnEnvelope", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ReturnGeometry", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task QueryAsync_Throws_WhenLayerDoesNotSupportReturningGeometryEnvelope() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var requestUris = new List<string>();

        var client = CreateClient(request => {
            requestUris.Add(request.RequestUri!.AbsoluteUri);

            if (IsLayerMetadataRequest(request)) {
                return CreateLayerMetadataResponse(
                    supportsPagination: true,
                    supportsReturningGeometryEnvelope: false);
            }

            throw new InvalidOperationException("HTTP should not be called after schema lookup.");
        });

        var layerClient = client.GetLayerClient(0);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () => {
            await foreach (var _ in layerClient.QueryAsync(
                new FeatureQuery {
                    ReturnEnvelope = true
                },
                cancellationToken)) {
            }
        });

        Assert.Contains("returning geometry envelopes", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Single(requestUris);
    }

    private static FeatureServiceClient CreateClient(Func<HttpRequestMessage, HttpResponseMessage> handler) {
        return new FeatureServiceClient(
            new HttpClient(new StubHttpMessageHandler(handler)),
            new FeatureServiceClientOptions {
                ServiceUri = new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer"),
                QueryRequestMethodPreference = QueryRequestMethodPreference.Get
            });
    }

    private static bool IsLayerMetadataRequest(HttpRequestMessage request) {
        return request.RequestUri?.AbsolutePath.EndsWith(
            "/FeatureServer/0",
            StringComparison.OrdinalIgnoreCase) == true;
    }

    private static HttpResponseMessage CreateLayerMetadataResponse(
        bool supportsPagination,
        bool supportsReturningGeometryEnvelope) {
        return StubHttpMessageHandler.Json($$"""
        {
          "id": 0,
          "name": "Layer 0",
          "geometryType": "esriGeometryPolygon",
          "objectIdField": "OBJECTID",
          "maxRecordCount": 1000,
          "advancedQueryCapabilities": {
            "supportsPagination": {{supportsPagination.ToString().ToLowerInvariant()}},
            "supportsReturningGeometryEnvelope": {{supportsReturningGeometryEnvelope.ToString().ToLowerInvariant()}}
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