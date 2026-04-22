using S100Framework.REST.Clients;
using S100Framework.REST.Configuration;
using S100Framework.REST.Tests.TestDoubles;
using Xunit;

namespace S100Framework.REST.Tests.Clients;

public sealed class FeatureServiceClientLayerLookupTests
{
    [Fact]
    public async Task GetLayerClientAsync_ResolvesLayerByName() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var handler = new StubHttpMessageHandler(request => {
            var uri = request.RequestUri!.AbsoluteUri;

            if (uri.EndsWith("/FeatureServer?f=json", StringComparison.Ordinal)) {
                return StubHttpMessageHandler.Json("""
                {
                  "layers": [
                    { "id": 0, "name": "Facilities" },
                    { "id": 1, "name": "Harbors" }
                  ],
                  "tables": []
                }
                """);
            }

            if (uri.Contains("/FeatureServer/1?")) {
                return StubHttpMessageHandler.Json("""
                {
                  "id": 1,
                  "name": "Harbors",
                  "geometryType": "esriGeometryPoint",
                  "objectIdField": "OBJECTID",
                  "maxRecordCount": 1000,
                  "advancedQueryCapabilities": {
                    "supportsPagination": true
                  },
                  "fields": [
                    { "name": "OBJECTID", "type": "esriFieldTypeOID", "nullable": false }
                  ],
                  "relationships": [],
                  "hasAttachments": false,
                  "supportsQueryAttachments": false,
                  "supportsAttachmentsResizing": false,
                  "supportsTopFeaturesQuery": false,
                  "extent": {
                    "spatialReference": { "wkid": 4326, "latestWkid": 4326 }
                  }
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {uri}");
        });

        var client = new FeatureServiceClient(
            new HttpClient(handler),
            new FeatureServiceClientOptions {
                ServiceUri = new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer")
            });

        var layerClient = await client.GetLayerClientAsync("harbors", cancellationToken);
        var schema = await layerClient.GetSchemaAsync(cancellationToken);

        Assert.Equal("Harbors", schema.Name);
        Assert.Equal(1, schema.LayerId);
    }

    [Fact]
    public async Task GetLayerClientAsync_Throws_WhenLayerNameDoesNotExist() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var handler = new StubHttpMessageHandler(request => {
            var uri = request.RequestUri!.AbsoluteUri;

            if (uri.EndsWith("/FeatureServer?f=json", StringComparison.Ordinal)) {
                return StubHttpMessageHandler.Json("""
                {
                  "layers": [
                    { "id": 0, "name": "Facilities" }
                  ],
                  "tables": []
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {uri}");
        });

        var client = new FeatureServiceClient(
            new HttpClient(handler),
            new FeatureServiceClientOptions {
                ServiceUri = new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer")
            });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.GetLayerClientAsync("Missing", cancellationToken));

        Assert.Contains("Missing", exception.Message);
    }

    [Fact]
    public async Task GetLayerClientAsync_ResolvesTableByName_WhenOnlyTableMatches() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var handler = new StubHttpMessageHandler(request => {
            var uri = request.RequestUri!.AbsoluteUri;

            if (uri.EndsWith("/FeatureServer?f=json", StringComparison.Ordinal)) {
                return StubHttpMessageHandler.Json("""
                {
                  "layers": [
                    { "id": 0, "name": "Facilities" },
                    { "id": 1, "name": "Harbors" }
                  ],
                  "tables": [
                    { "id": 7, "name": "HarborCodes" }
                  ]
                }
                """);
            }

            if (uri.Contains("/FeatureServer/7?")) {
                return StubHttpMessageHandler.Json("""
                {
                  "id": 7,
                  "name": "HarborCodes",
                  "geometryType": "esriGeometryPoint",
                  "objectIdField": "OBJECTID",
                  "maxRecordCount": 1000,
                  "advancedQueryCapabilities": {
                    "supportsPagination": true
                  },
                  "fields": [
                    { "name": "OBJECTID", "type": "esriFieldTypeOID", "nullable": false }
                  ],
                  "relationships": [],
                  "hasAttachments": false,
                  "supportsQueryAttachments": false,
                  "supportsAttachmentsResizing": false,
                  "supportsTopFeaturesQuery": false,
                  "extent": {
                    "spatialReference": { "wkid": 4326, "latestWkid": 4326 }
                  }
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {uri}");
        });

        var client = new FeatureServiceClient(
            new HttpClient(handler),
            new FeatureServiceClientOptions {
                ServiceUri = new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer")
            });

        var layerClient = await client.GetLayerClientAsync("HarborCodes", cancellationToken);
        var schema = await layerClient.GetSchemaAsync(cancellationToken);

        Assert.Equal("HarborCodes", schema.Name);
        Assert.Equal(7, schema.LayerId);
    }

    [Fact]
    public async Task GetLayerClientAsync_ResolvesTableByName_CaseInsensitive() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var handler = new StubHttpMessageHandler(request => {
            var uri = request.RequestUri!.AbsoluteUri;

            if (uri.EndsWith("/FeatureServer?f=json", StringComparison.Ordinal)) {
                return StubHttpMessageHandler.Json("""
                {
                  "layers": [],
                  "tables": [
                    { "id": 9, "name": "StatusLookup" }
                  ]
                }
                """);
            }

            if (uri.Contains("/FeatureServer/9?")) {
                return StubHttpMessageHandler.Json("""
                {
                  "id": 9,
                  "name": "StatusLookup",
                  "geometryType": "esriGeometryPoint",
                  "objectIdField": "OBJECTID",
                  "maxRecordCount": 1000,
                  "advancedQueryCapabilities": {
                    "supportsPagination": true
                  },
                  "fields": [
                    { "name": "OBJECTID", "type": "esriFieldTypeOID", "nullable": false }
                  ],
                  "relationships": [],
                  "hasAttachments": false,
                  "supportsQueryAttachments": false,
                  "supportsAttachmentsResizing": false,
                  "supportsTopFeaturesQuery": false,
                  "extent": {
                    "spatialReference": { "wkid": 4326, "latestWkid": 4326 }
                  }
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {uri}");
        });

        var client = new FeatureServiceClient(
            new HttpClient(handler),
            new FeatureServiceClientOptions {
                ServiceUri = new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer")
            });

        var layerClient = await client.GetLayerClientAsync("statuslookup", cancellationToken);
        var schema = await layerClient.GetSchemaAsync(cancellationToken);

        Assert.Equal("StatusLookup", schema.Name);
        Assert.Equal(9, schema.LayerId);
    }

    [Fact]
    public async Task GetLayerClientAsync_Throws_WhenLayerNameIsWhitespace() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = new FeatureServiceClient(
            new HttpClient(new StubHttpMessageHandler(_ =>
                throw new InvalidOperationException("The HTTP request should not be executed."))),
            new FeatureServiceClientOptions {
                ServiceUri = new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer")
            });

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            client.GetLayerClientAsync(" ", cancellationToken));

        Assert.Equal("layerName", exception.ParamName);
        Assert.Contains("Layer name must be provided", exception.Message);
    }

    [Fact]
    public async Task GetLayerClientAsync_Throws_WhenMultipleDatasetsMatchName() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var handler = new StubHttpMessageHandler(request => {
            var uri = request.RequestUri!.AbsoluteUri;

            if (uri.EndsWith("/FeatureServer?f=json", StringComparison.Ordinal)) {
                return StubHttpMessageHandler.Json("""
                {
                  "layers": [
                    { "id": 0, "name": "SharedName" }
                  ],
                  "tables": [
                    { "id": 7, "name": "SharedName" }
                  ]
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {uri}");
        });

        var client = new FeatureServiceClient(
            new HttpClient(handler),
            new FeatureServiceClientOptions {
                ServiceUri = new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer")
            });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.GetLayerClientAsync("sharedname", cancellationToken));

        Assert.Contains("Multiple layers or tables named 'sharedname' were found", exception.Message);
        Assert.Contains("Use the layer ID instead", exception.Message);
    }

    [Fact]
    public void GetLayerClient_Throws_WhenLayerIdIsNegative() {
        var client = new FeatureServiceClient(
            new HttpClient(new StubHttpMessageHandler(_ =>
                throw new InvalidOperationException("The HTTP request should not be executed."))),
            new FeatureServiceClientOptions {
                ServiceUri = new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer")
            });

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => client.GetLayerClient(-1));

        Assert.Equal("layerId", exception.ParamName);
    }
}