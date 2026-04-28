using System.Text.Json;
using S100Framework.REST.Clients;
using S100Framework.REST.Configuration;
using S100Framework.REST.Tests.TestDoubles;
using Xunit;

namespace S100Framework.REST.Tests.Clients;

public sealed class FeatureLayerClientQueryDataElementsTests
{
    [Fact]
    public async Task QueryDataElementAsync_ReturnsCurrentLayerDataElement()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var requestUris = new List<Uri>();

        var client = CreateClient(request => {
            requestUris.Add(request.RequestUri!);

            if (request.RequestUri!.AbsolutePath.EndsWith(
                "/FeatureServer",
                StringComparison.OrdinalIgnoreCase))
            {
                return CreateServiceMetadataResponse(supportsQueryDataElements: true);
            }

            if (request.RequestUri!.AbsolutePath.EndsWith(
                "/FeatureServer/queryDataElements",
                StringComparison.OrdinalIgnoreCase))
            {
                return StubHttpMessageHandler.Json("""
                {
                  "layerDataElements": [
                    {
                      "layerId": 2,
                      "dataElement": {
                        "name": "Layer 2",
                        "datasetType": "esriDTFeatureClass",
                        "versioned": false
                      }
                    }
                  ]
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var layerClient = client.GetLayerClient(2);

        var dataElement = await layerClient.QueryDataElementAsync(cancellationToken);

        Assert.Equal(2, dataElement.LayerId);
        Assert.Equal("Layer 2", dataElement.DataElement.GetProperty("name").GetString());
        Assert.Equal("esriDTFeatureClass", dataElement.DataElement.GetProperty("datasetType").GetString());
        Assert.False(dataElement.DataElement.GetProperty("versioned").GetBoolean());

        var queryDataElementsRequest = Assert.Single(
            requestUris,
            uri => uri.AbsolutePath.EndsWith(
                "/FeatureServer/queryDataElements",
                StringComparison.OrdinalIgnoreCase));

        var query = ParseQuery(queryDataElementsRequest);

        Assert.Equal("json", query["f"]);
        Assert.Equal("[2]", query["layers"]);
    }

    private static FeatureServiceClient CreateClient(Func<HttpRequestMessage, HttpResponseMessage> handler)
    {
        return new FeatureServiceClient(
            new HttpClient(new StubHttpMessageHandler(handler)),
            new FeatureServiceClientOptions
            {
                ServiceUri = new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer")
            });
    }

    private static HttpResponseMessage CreateServiceMetadataResponse(bool supportsQueryDataElements)
    {
        return StubHttpMessageHandler.Json($$"""
        {
          "layers": [
            { "id": 2, "name": "Layer 2" }
          ],
          "tables": [],
          "capabilities": "Query",
          "supportsQueryDataElements": {{supportsQueryDataElements.ToString().ToLowerInvariant()}}
        }
        """);
    }

    private static Dictionary<string, string> ParseQuery(Uri uri)
    {
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