using S100Framework.REST.Clients;
using S100Framework.REST.Configuration;
using S100Framework.REST.Exceptions;
using S100Framework.REST.Tests.TestDoubles;
using Xunit;

namespace S100Framework.REST.Tests.Clients;

public sealed class FeatureServiceClientTests
{
    [Fact]
    public async Task GetMetadataAsync_MapsLayersAndTables() {
        var handler = new StubHttpMessageHandler(_ =>
            StubHttpMessageHandler.Json("""
            {
              "layers": [
                { "id": 0, "name": "DepthAreas" }
              ],
              "tables": [
                { "id": 1, "name": "MetadataTable" }
              ],
              "capabilities": "Query",
              "maxRecordCount": 2000
            }
            """));

        var client = new FeatureServiceClient(
            new HttpClient(handler),
            new FeatureServiceClientOptions {
                ServiceUri = new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer")
            });

        var metadata = await client.GetMetadataAsync();

        Assert.Single(metadata.Layers);
        Assert.Single(metadata.Tables);
        Assert.Equal("DepthAreas", metadata.Layers[0].Name);
        Assert.Equal("MetadataTable", metadata.Tables[0].Name);
        Assert.Equal(2000, metadata.MaxRecordCount);
    }

    [Fact]
    public async Task GetMetadataAsync_ThrowsFeatureServiceException_WhenEsriErrorPayloadReturned() {
        var handler = new StubHttpMessageHandler(_ =>
            StubHttpMessageHandler.Json("""
            {
              "error": {
                "code": 400,
                "message": "Invalid or missing input parameters.",
                "details": [ "f is required" ]
              }
            }
            """, System.Net.HttpStatusCode.BadRequest));

        var client = new FeatureServiceClient(
            new HttpClient(handler),
            new FeatureServiceClientOptions {
                ServiceUri = new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer")
            });

        var exception = await Assert.ThrowsAsync<FeatureServiceException>(() => client.GetMetadataAsync());

        Assert.Equal(400, exception.ErrorCode);
        Assert.Contains("f is required", exception.Details);
    }
}