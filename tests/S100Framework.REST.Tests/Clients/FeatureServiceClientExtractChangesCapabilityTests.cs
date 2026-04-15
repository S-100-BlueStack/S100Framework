using S100Framework.REST.Clients;
using S100Framework.REST.Configuration;
using S100Framework.REST.Exceptions;
using S100Framework.REST.Models;
using S100Framework.REST.Tests.TestDoubles;
using System.Net.Http;
using Xunit;

namespace S100Framework.REST.Tests.Clients;

public sealed class FeatureServiceClientExtractChangesCapabilityTests
{
    [Fact]
    public async Task SubmitExtractChangesAsync_Throws_WhenServiceDoesNotSupportChangeTracking() {
        var client = CreateClient(request => {
            if (IsServiceMetadataRequest(request)) {
                return CreateServiceMetadataResponse("Query");
            }

            throw new InvalidOperationException("extractChanges endpoint should not be called.");
        });

        var exception = await Assert.ThrowsAsync<FeatureServiceCapabilityException>(() =>
            client.SubmitExtractChangesAsync(new ExtractChangesRequest {
                Layers = [0],
                LayerServerGens = [new ExtractChangesLayerServerGen(0, 1653608093000)]
            }));

        Assert.Contains("change tracking", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SubmitExtractChangesAsync_Throws_WhenLayerQueriesAreNotSupported() {
        var client = CreateClient(request => {
            if (IsServiceMetadataRequest(request)) {
                return CreateServiceMetadataResponse(
                    "Query,ChangeTracking",
                    """
                    {
                      "supportsReturnIdsOnly": true,
                      "supportsReturnExtentOnly": true,
                      "supportsReturnAttachments": true,
                      "supportsLayerQueries": false,
                      "supportsGeometry": true,
                      "supportsReturnFeature": true,
                      "supportsFieldsToCompare": true,
                      "supportsServerGens": true,
                      "supportsReturnHasGeometryUpdates": true
                    }
                    """);
            }

            throw new InvalidOperationException("extractChanges endpoint should not be called.");
        });

        var exception = await Assert.ThrowsAsync<FeatureServiceCapabilityException>(() =>
            client.SubmitExtractChangesAsync(new ExtractChangesRequest {
                Layers = [0],
                LayerServerGens = [new ExtractChangesLayerServerGen(0, 1653608093000)],
                LayerQueries = new Dictionary<int, ExtractChangesLayerQuery> {
                    [0] = new() {
                        QueryOption = ExtractChangesLayerQueryOption.UseFilter,
                        Where = "1=1"
                    }
                }
            }));

        Assert.Contains("layerQueries", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SubmitExtractChangesAsync_Throws_WhenFieldsToCompareAreNotSupported() {
        var client = CreateClient(request => {
            if (IsServiceMetadataRequest(request)) {
                return CreateServiceMetadataResponse(
                    "Query,ChangeTracking",
                    """
                    {
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
                    """);
            }

            throw new InvalidOperationException("extractChanges endpoint should not be called.");
        });

        var exception = await Assert.ThrowsAsync<FeatureServiceCapabilityException>(() =>
            client.SubmitExtractChangesAsync(new ExtractChangesRequest {
                Layers = [0],
                LayerServerGens = [new ExtractChangesLayerServerGen(0, 1653608093000)],
                FieldsToCompare = ["STATUS"]
            }));

        Assert.Contains("fieldsToCompare", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static FeatureServiceClient CreateClient(Func<HttpRequestMessage, HttpResponseMessage> handler) {
        return new FeatureServiceClient(
            new HttpClient(new StubHttpMessageHandler(handler)),
            new FeatureServiceClientOptions {
                ServiceUri = new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer")
            });
    }

    private static bool IsServiceMetadataRequest(HttpRequestMessage request) {
        return request.RequestUri?.AbsolutePath.EndsWith(
            "/FeatureServer",
            StringComparison.OrdinalIgnoreCase) == true;
    }

    private static HttpResponseMessage CreateServiceMetadataResponse(
        string capabilities,
        string extractChangesCapabilitiesJson =
            """
            {
              "supportsReturnIdsOnly": true,
              "supportsReturnExtentOnly": true,
              "supportsReturnAttachments": true,
              "supportsLayerQueries": true,
              "supportsGeometry": true,
              "supportsReturnFeature": true,
              "supportsFieldsToCompare": true,
              "supportsServerGens": true,
              "supportsReturnHasGeometryUpdates": true
            }
            """) {
        return StubHttpMessageHandler.Json($$"""
        {
          "capabilities": "{{capabilities}}",
          "layers": [
            { "id": 0, "name": "Layer 0" }
          ],
          "tables": [],
          "extractChangesCapabilities": {{extractChangesCapabilitiesJson}}
        }
        """);
    }
}