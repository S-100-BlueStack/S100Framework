using S100Framework.REST.Clients;
using S100Framework.REST.Configuration;
using S100Framework.REST.Tests.TestDoubles;
using Xunit;

namespace S100Framework.REST.Tests.Clients;

public sealed class FeatureServiceMetadataCapabilitiesTests
{
    [Fact]
    public async Task GetMetadataAsync_MapsServiceAndExtractChangesCapabilities() {
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

        var metadata = await client.GetMetadataAsync();

        Assert.True(metadata.CapabilityInfo.SupportsQuery);
        Assert.True(metadata.CapabilityInfo.SupportsCreate);
        Assert.True(metadata.CapabilityInfo.SupportsUpdate);
        Assert.True(metadata.CapabilityInfo.SupportsDelete);
        Assert.True(metadata.CapabilityInfo.SupportsEditing);
        Assert.True(metadata.CapabilityInfo.SupportsUploads);
        Assert.True(metadata.CapabilityInfo.SupportsChangeTracking);
        Assert.False(metadata.CapabilityInfo.SupportsSync);
        Assert.False(metadata.CapabilityInfo.SyncEnabled);
        Assert.True(metadata.CapabilityInfo.SupportsAsyncApplyEdits);

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
}