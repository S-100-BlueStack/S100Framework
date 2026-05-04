using System.Text.Json;
using S100Framework.REST.Clients;
using S100Framework.REST.Configuration;
using S100Framework.REST.Exceptions;
using S100Framework.REST.Tests.TestDoubles;
using Xunit;

namespace S100Framework.REST.Tests.Clients;

public sealed class FeatureServiceClientQueryDataElementsTests
{
    [Fact]
    public async Task QueryDataElementsAsync_SendsLayerIdsAndMapsDataElements() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var requestUris = new List<Uri>();

        var client = CreateClient(request => {
            requestUris.Add(request.RequestUri!);

            if (IsServiceMetadataRequest(request)) {
                return CreateServiceMetadataResponse(supportsQueryDataElements: true);
            }

            if (IsQueryDataElementsRequest(request)) {
                return StubHttpMessageHandler.Json("""
                {
                  "layerDataElements": [
                    {
                      "layerId": 0,
                      "dataElement": {
                        "name": "Layer 0",
                        "datasetType": "esriDTFeatureClass"
                      }
                    },
                    {
                      "layerId": 1,
                      "dataElement": {
                        "name": "Table 1",
                        "datasetType": "esriDTTable"
                      }
                    }
                  ]
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var dataElements = await client.QueryDataElementsAsync([0, 1], cancellationToken);

        Assert.Equal(2, dataElements.Count);
        Assert.Equal(0, dataElements[0].LayerId);
        Assert.Equal("Layer 0", dataElements[0].DataElement.GetProperty("name").GetString());
        Assert.Equal("esriDTFeatureClass", dataElements[0].DataElement.GetProperty("datasetType").GetString());

        Assert.Equal(1, dataElements[1].LayerId);
        Assert.Equal("Table 1", dataElements[1].DataElement.GetProperty("name").GetString());
        Assert.Equal("esriDTTable", dataElements[1].DataElement.GetProperty("datasetType").GetString());

        var queryDataElementsRequest = Assert.Single(
            requestUris,
            uri => uri.AbsolutePath.EndsWith(
                "/FeatureServer/queryDataElements",
                StringComparison.OrdinalIgnoreCase));

        var query = ParseQuery(queryDataElementsRequest);

        Assert.Equal("json", query["f"]);
        Assert.Equal("[0,1]", query["layers"]);
    }

    [Fact]
    public async Task QueryDataElementsAsync_ReturnsEmptyList_WhenLayerDataElementsPropertyIsMissing() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsServiceMetadataRequest(request)) {
                return CreateServiceMetadataResponse(supportsQueryDataElements: true);
            }

            if (IsQueryDataElementsRequest(request)) {
                return StubHttpMessageHandler.Json("{}");
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var dataElements = await client.QueryDataElementsAsync([0], cancellationToken);

        Assert.Empty(dataElements);
    }

    [Fact]
    public async Task QueryDataElementsAsync_ReturnsEmptyList_WhenLayerDataElementsPropertyIsNull() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsServiceMetadataRequest(request)) {
                return CreateServiceMetadataResponse(supportsQueryDataElements: true);
            }

            if (IsQueryDataElementsRequest(request)) {
                return StubHttpMessageHandler.Json("""
                {
                  "layerDataElements": null
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var dataElements = await client.QueryDataElementsAsync([0], cancellationToken);

        Assert.Empty(dataElements);
    }

    [Fact]
    public async Task QueryDataElementsAsync_IgnoresNullLayerDataElementItems() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsServiceMetadataRequest(request)) {
                return CreateServiceMetadataResponse(supportsQueryDataElements: true);
            }

            if (IsQueryDataElementsRequest(request)) {
                return StubHttpMessageHandler.Json("""
                {
                  "layerDataElements": [
                    null,
                    {
                      "layerId": 0,
                      "dataElement": {
                        "name": "Layer 0"
                      }
                    }
                  ]
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var dataElement = Assert.Single(await client.QueryDataElementsAsync([0], cancellationToken));

        Assert.Equal(0, dataElement.LayerId);
        Assert.Equal("Layer 0", dataElement.DataElement.GetProperty("name").GetString());
    }

    [Fact]
    public async Task QueryDataElementsAsync_Throws_WhenReturnedDataElementOmitsLayerId() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsServiceMetadataRequest(request)) {
                return CreateServiceMetadataResponse(supportsQueryDataElements: true);
            }

            if (IsQueryDataElementsRequest(request)) {
                return StubHttpMessageHandler.Json("""
                {
                  "layerDataElements": [
                    {
                      "dataElement": {
                        "name": "Layer without id"
                      }
                    }
                  ]
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var exception = await Assert.ThrowsAsync<FeatureServiceException>(() =>
            client.QueryDataElementsAsync([0], cancellationToken));

        Assert.Contains("layer ID", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task QueryDataElementsAsync_ReturnsUndefinedDataElement_WhenServerOmitsDataElementPayload() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsServiceMetadataRequest(request)) {
                return CreateServiceMetadataResponse(supportsQueryDataElements: true);
            }

            if (IsQueryDataElementsRequest(request)) {
                return StubHttpMessageHandler.Json("""
                {
                  "layerDataElements": [
                    {
                      "layerId": 0
                    }
                  ]
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var dataElement = Assert.Single(await client.QueryDataElementsAsync([0], cancellationToken));

        Assert.Equal(0, dataElement.LayerId);
        Assert.Equal(JsonValueKind.Undefined, dataElement.DataElement.ValueKind);
    }

    [Fact]
    public async Task QueryDataElementsAsync_Throws_WhenServiceDoesNotAdvertiseSupport() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsServiceMetadataRequest(request)) {
                return CreateServiceMetadataResponse(supportsQueryDataElements: false);
            }

            throw new InvalidOperationException("HTTP should not be called after metadata lookup.");
        });

        var exception = await Assert.ThrowsAsync<FeatureServiceCapabilityException>(() =>
            client.QueryDataElementsAsync([0], cancellationToken));

        Assert.Contains("queryDataElements", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task QueryDataElementsAsync_Throws_WhenLayerIdsContainDuplicates() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(_ =>
            throw new InvalidOperationException("HTTP should not be called."));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.QueryDataElementsAsync([0, 0], cancellationToken));

        Assert.Contains("duplicate", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetMetadataAsync_MapsQueryDataElementsCapability() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsServiceMetadataRequest(request)) {
                return CreateServiceMetadataResponse(supportsQueryDataElements: true);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var metadata = await client.GetMetadataAsync(cancellationToken);

        Assert.True(metadata.Capabilities.SupportsQueryDataElements);
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

    private static bool IsQueryDataElementsRequest(HttpRequestMessage request) {
        return request.RequestUri?.AbsolutePath.EndsWith(
            "/FeatureServer/queryDataElements",
            StringComparison.OrdinalIgnoreCase) == true;
    }

    private static HttpResponseMessage CreateServiceMetadataResponse(bool supportsQueryDataElements) {
        return StubHttpMessageHandler.Json($$"""
        {
          "layers": [
            { "id": 0, "name": "Layer 0" },
            { "id": 1, "name": "Table 1" }
          ],
          "tables": [],
          "capabilities": "Query",
          "supportsQueryDataElements": {{supportsQueryDataElements.ToString().ToLowerInvariant()}}
        }
        """);
    }

    private static Dictionary<string, string> ParseQuery(Uri uri) {
        return uri.Query
            .TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Split('=', 2))
            .ToDictionary(
                parts => Uri.UnescapeDataString(parts[0]),
                parts => parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : string.Empty,
                StringComparer.Ordinal);
    }
}