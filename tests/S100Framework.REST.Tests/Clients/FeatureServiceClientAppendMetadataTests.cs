using S100Framework.REST.Clients;
using S100Framework.REST.Configuration;
using S100Framework.REST.Tests.TestDoubles;
using Xunit;

namespace S100Framework.REST.Tests.Clients;

public sealed class FeatureServiceClientAppendMetadataTests
{
    [Fact]
    public async Task GetMetadataAsync_MapsAppendCapabilitiesAndFormats_WhenServiceAdvertisesThem() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var handler = new StubHttpMessageHandler(_ => StubHttpMessageHandler.Json("""
        {
          "layers": [
            { "id": 0, "name": "DepthAreas" }
          ],
          "tables": [],
          "capabilities": "Query,Uploads",
          "maxRecordCount": 2000,
          "syncEnabled": false,
          "supportsAppend": true,
          "supportedAppendFormats": [
            "sqlite",
            "feature Service",
            "pbf"
          ]
        }
        """));

        var client = new FeatureServiceClient(
            new HttpClient(handler),
            new FeatureServiceClientOptions {
                ServiceUri = new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer")
            });

        var metadata = await client.GetMetadataAsync(cancellationToken);

        Assert.True(metadata.Capabilities.SupportsAppend);
        Assert.Equal(["sqlite", "feature Service", "pbf"], metadata.SupportedAppendFormats);
    }

    [Fact]
    public async Task GetMetadataAsync_DefaultsAppendMetadata_WhenServiceDoesNotAdvertiseIt() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var handler = new StubHttpMessageHandler(_ => StubHttpMessageHandler.Json("""
        {
          "layers": [],
          "tables": [],
          "capabilities": "Query",
          "maxRecordCount": 1000,
          "syncEnabled": true
        }
        """));

        var client = new FeatureServiceClient(
            new HttpClient(handler),
            new FeatureServiceClientOptions {
                ServiceUri = new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer")
            });

        var metadata = await client.GetMetadataAsync(cancellationToken);

        Assert.False(metadata.Capabilities.SupportsAppend);
        Assert.Empty(metadata.SupportedAppendFormats);
    }

    [Fact]
    public async Task GetMetadataAsync_MapsSupportedAppendFormats_WhenServerReturnsCommaSeparatedString() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var handler = new StubHttpMessageHandler(_ => StubHttpMessageHandler.Json("""
    {
      "layers": [
        { "id": 0, "name": "DepthAreas" }
      ],
      "tables": [],
      "capabilities": "Query,Uploads",
      "maxRecordCount": 2000,
      "syncEnabled": false,
      "supportsAppend": true,
      "supportedAppendFormats": "sqlite, feature Service, pbf"
    }
    """));

        var client = new FeatureServiceClient(
            new HttpClient(handler),
            new FeatureServiceClientOptions {
                ServiceUri = new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer")
            });

        var metadata = await client.GetMetadataAsync(cancellationToken);

        Assert.True(metadata.Capabilities.SupportsAppend);
        Assert.Equal(["sqlite", "feature Service", "pbf"], metadata.SupportedAppendFormats);
    }
}