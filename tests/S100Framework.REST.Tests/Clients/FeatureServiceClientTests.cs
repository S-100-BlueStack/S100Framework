using S100Framework.REST.Clients;
using S100Framework.REST.Configuration;
using S100Framework.REST.Exceptions;
using S100Framework.REST.Tests.TestDoubles;
using Xunit;

namespace S100Framework.REST.Tests.Clients;

public sealed class FeatureServiceClientTests
{
    [Fact]
    public async Task GetMetadataAsync_MapsLayersTablesAndCapabilities() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var handler = new StubHttpMessageHandler(_ =>
            StubHttpMessageHandler.Json("""
            {
              "layers": [
                { "id": 0, "name": "DepthAreas" }
              ],
              "tables": [
                { "id": 1, "name": "MetadataTable" }
              ],
              "capabilities": "Query,ChangeTracking",
              "maxRecordCount": 2000,
              "syncEnabled": true
            }
            """));

        var client = new FeatureServiceClient(
            new HttpClient(handler),
            new FeatureServiceClientOptions {
                ServiceUri = new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer")
            });

        var metadata = await client.GetMetadataAsync(cancellationToken);

        Assert.Single(metadata.Layers);
        Assert.Single(metadata.Tables);
        Assert.Equal("DepthAreas", metadata.Layers[0].Name);
        Assert.Equal("MetadataTable", metadata.Tables[0].Name);
        Assert.Equal("Query,ChangeTracking", metadata.CapabilityText);
        Assert.Equal(2000, metadata.MaxRecordCount);
        Assert.True(metadata.Capabilities.SupportsQuery);
        Assert.True(metadata.Capabilities.SupportsChangeTracking);
        Assert.True(metadata.Capabilities.SyncEnabled);
    }

    [Fact]
    public async Task GetMetadataAsync_ThrowsFeatureServiceException_WhenEsriErrorPayloadReturned() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var handler = new StubHttpMessageHandler(_ =>
            StubHttpMessageHandler.Json("""
            {
              "error": {
                "code": 400,
                "message": "Invalid or missing input parameters.",
                "details": [
                  "f is required"
                ]
              }
            }
            """, System.Net.HttpStatusCode.BadRequest));

        var client = new FeatureServiceClient(
            new HttpClient(handler),
            new FeatureServiceClientOptions {
                ServiceUri = new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer")
            });

        var exception = await Assert.ThrowsAsync<FeatureServiceException>(() =>
            client.GetMetadataAsync(cancellationToken));

        Assert.Equal(400, exception.ErrorCode);
        Assert.Contains("f is required", exception.Details);
    }

    [Fact]
    public async Task GetMetadataAsync_UsesFallbackMessage_WhenEsriErrorMessageIsMissing() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var handler = new StubHttpMessageHandler(_ =>
            StubHttpMessageHandler.Json("""
        {
          "error": {
            "code": 500,
            "details": [
              "Internal server error"
            ]
          }
        }
        """, System.Net.HttpStatusCode.InternalServerError));

        var client = new FeatureServiceClient(
            new HttpClient(handler),
            new FeatureServiceClientOptions {
                ServiceUri = new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer")
            });

        var exception = await Assert.ThrowsAsync<FeatureServiceException>(() =>
            client.GetMetadataAsync(cancellationToken));

        Assert.Equal(500, exception.ErrorCode);
        Assert.Contains("The server returned an Esri error payload.", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Internal server error", exception.Details);
    }

    [Fact]
    public async Task GetMetadataAsync_UsesFallbackMessage_WhenEsriErrorMessageIsBlank() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var handler = new StubHttpMessageHandler(_ =>
            StubHttpMessageHandler.Json("""
        {
          "error": {
            "code": 400,
            "message": "   ",
            "details": [
              "Invalid request"
            ]
          }
        }
        """, System.Net.HttpStatusCode.BadRequest));

        var client = new FeatureServiceClient(
            new HttpClient(handler),
            new FeatureServiceClientOptions {
                ServiceUri = new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer")
            });

        var exception = await Assert.ThrowsAsync<FeatureServiceException>(() =>
            client.GetMetadataAsync(cancellationToken));

        Assert.Equal(400, exception.ErrorCode);
        Assert.Contains("The server returned an Esri error payload.", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Invalid request", exception.Details);
    }

    [Fact]
    public async Task GetMetadataAsync_FiltersEmptyEsriErrorDetails() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var handler = new StubHttpMessageHandler(_ =>
            StubHttpMessageHandler.Json("""
        {
          "error": {
            "code": 400,
            "message": "Invalid request.",
            "details": [
              null,
              "",
              "   ",
              "f is required"
            ]
          }
        }
        """, System.Net.HttpStatusCode.BadRequest));

        var client = new FeatureServiceClient(
            new HttpClient(handler),
            new FeatureServiceClientOptions {
                ServiceUri = new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer")
            });

        var exception = await Assert.ThrowsAsync<FeatureServiceException>(() =>
            client.GetMetadataAsync(cancellationToken));

        var detail = Assert.Single(exception.Details);

        Assert.Equal("f is required", detail);
    }
}