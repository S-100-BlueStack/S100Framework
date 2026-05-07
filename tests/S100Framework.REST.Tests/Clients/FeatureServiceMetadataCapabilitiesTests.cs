using S100Framework.REST.Clients;
using S100Framework.REST.Configuration;
using S100Framework.REST.Exceptions;
using S100Framework.REST.Tests.TestDoubles;
using Xunit;

namespace S100Framework.REST.Tests.Clients;

public sealed class FeatureServiceMetadataCapabilitiesTests
{
    [Fact]
    public async Task GetMetadataAsync_MapsServiceAndExtractChangesCapabilities() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var handler = new StubHttpMessageHandler(request => {
            var uri = request.RequestUri!.AbsoluteUri;

            if (uri.EndsWith("/FeatureServer?f=json", StringComparison.Ordinal)) {
                return StubHttpMessageHandler.Json("""
                {
                  "layers": [
                    { "id": 0, "name": "Facilities" }
                  ],
                  "tables": [],
                  "capabilities": "Create,Delete,Query,Update,Editing,Uploads,ChangeTracking",
                  "maxRecordCount": 2000,
                  "syncEnabled": false,
                  "advancedEditingCapabilities": {
                    "supportsAsyncApplyEdits": true
                  },
                  "extractChangesCapabilities": {
                    "supportsReturnIdsOnly": true,
                    "supportsReturnExtentOnly": true,
                    "supportsReturnAttachments": true,
                    "supportsLayerQueries": true,
                    "supportsGeometry": true,
                    "supportsReturnFeature": true,
                    "supportsFieldsToCompare": false,
                    "supportsServerGens": true,
                    "supportsReturnHasGeometryUpdates": true
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

        var metadata = await client.GetMetadataAsync(cancellationToken);

        Assert.True(metadata.Capabilities.SupportsQuery);
        Assert.True(metadata.Capabilities.SupportsCreate);
        Assert.True(metadata.Capabilities.SupportsUpdate);
        Assert.True(metadata.Capabilities.SupportsDelete);
        Assert.True(metadata.Capabilities.SupportsEditing);
        Assert.True(metadata.Capabilities.SupportsUploads);
        Assert.True(metadata.Capabilities.SupportsChangeTracking);
        Assert.False(metadata.Capabilities.SupportsSync);
        Assert.False(metadata.Capabilities.SyncEnabled);
        Assert.True(metadata.Capabilities.SupportsAsyncApplyEdits);

        Assert.NotNull(metadata.ExtractChangesCapabilities);
        Assert.True(metadata.ExtractChangesCapabilities!.SupportsReturnIdsOnly);
        Assert.True(metadata.ExtractChangesCapabilities.SupportsReturnExtentOnly);
        Assert.True(metadata.ExtractChangesCapabilities.SupportsReturnAttachments);
        Assert.True(metadata.ExtractChangesCapabilities.SupportsLayerQueries);
        Assert.True(metadata.ExtractChangesCapabilities.SupportsGeometry);
        Assert.True(metadata.ExtractChangesCapabilities.SupportsReturnFeature);
        Assert.False(metadata.ExtractChangesCapabilities.SupportsFieldsToCompare);
        Assert.True(metadata.ExtractChangesCapabilities.SupportsServerGens);
        Assert.True(metadata.ExtractChangesCapabilities.SupportsReturnHasGeometryUpdates);
    }

    [Fact]
    public async Task GetMetadataAsync_IgnoresNullLayerAndTableItems() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var handler = new StubHttpMessageHandler(request => {
            var uri = request.RequestUri!.AbsoluteUri;

            if (uri.EndsWith("/FeatureServer?f=json", StringComparison.Ordinal)) {
                return StubHttpMessageHandler.Json("""
            {
              "layers": [
                null,
                { "id": 0, "name": "Facilities" },
                null
              ],
              "tables": [
                null,
                { "id": 1, "name": "Inspections" },
                null
              ],
              "capabilities": "Query",
              "maxRecordCount": 2000
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

        var metadata = await client.GetMetadataAsync(cancellationToken);

        var layer = Assert.Single(metadata.Layers);
        var table = Assert.Single(metadata.Tables);

        Assert.Equal(0, layer.Id);
        Assert.Equal("Facilities", layer.Name);
        Assert.Equal(1, table.Id);
        Assert.Equal("Inspections", table.Name);
    }

    [Fact]
    public async Task GetMetadataAsync_ThrowsFeatureServiceException_WhenLayerIdIsMissing() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var handler = new StubHttpMessageHandler(request => {
            var uri = request.RequestUri!.AbsoluteUri;

            if (uri.EndsWith("/FeatureServer?f=json", StringComparison.Ordinal)) {
                return StubHttpMessageHandler.Json("""
            {
              "layers": [
                { "name": "Facilities" }
              ],
              "tables": [],
              "capabilities": "Query"
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

        var exception = await Assert.ThrowsAsync<FeatureServiceException>(() =>
            client.GetMetadataAsync(cancellationToken));

        Assert.Contains("layers", exception.Message, StringComparison.Ordinal);
        Assert.Contains("ID", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetMetadataAsync_ThrowsFeatureServiceException_WhenTableIdIsNegative() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var handler = new StubHttpMessageHandler(request => {
            var uri = request.RequestUri!.AbsoluteUri;

            if (uri.EndsWith("/FeatureServer?f=json", StringComparison.Ordinal)) {
                return StubHttpMessageHandler.Json("""
            {
              "layers": [],
              "tables": [
                { "id": -1, "name": "Inspections" }
              ],
              "capabilities": "Query"
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

        var exception = await Assert.ThrowsAsync<FeatureServiceException>(() =>
            client.GetMetadataAsync(cancellationToken));

        Assert.Contains("tables", exception.Message, StringComparison.Ordinal);
        Assert.Contains("negative", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}