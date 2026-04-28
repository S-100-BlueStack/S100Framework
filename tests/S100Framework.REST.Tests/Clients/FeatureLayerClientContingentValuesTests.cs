using S100Framework.REST.Clients;
using S100Framework.REST.Configuration;
using S100Framework.REST.Models;
using S100Framework.REST.Tests.TestDoubles;
using Xunit;

namespace S100Framework.REST.Tests.Clients;

public sealed class FeatureLayerClientContingentValuesTests
{
    [Fact]
    public async Task QueryContingentValuesAsync_QueriesCurrentLayerThroughServiceEndpoint() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var requestUris = new List<Uri>();

        var client = CreateClient(request => {
            requestUris.Add(request.RequestUri!);

            if (request.RequestUri!.AbsolutePath.EndsWith(
                "/FeatureServer",
                StringComparison.OrdinalIgnoreCase)) {
                return CreateServiceMetadataResponse(supportsQueryContingentValues: true);
            }

            if (request.RequestUri!.AbsolutePath.EndsWith(
                "/FeatureServer/queryContingentValues",
                StringComparison.OrdinalIgnoreCase)) {
                return StubHttpMessageHandler.Json("""
                {
                  "layers": [
                    {
                      "id": 2,
                      "contingentValuesDefinition": {
                        "fieldGroups": [
                          {
                            "name": "make_rating"
                          }
                        ]
                      }
                    }
                  ]
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var result = await client.GetLayerClient(2).QueryContingentValuesAsync(
            new QueryContingentValuesOptions {
                CompactFormat = false,
                DomainDictionaries = QueryContingentValuesDomainDictionaries.Trimmed
            },
            cancellationToken);

        Assert.Equal(2, result.LayerId);
        Assert.True(result.Payload.TryGetProperty("layers", out _));

        var requestUri = Assert.Single(
            requestUris,
            uri => uri.AbsolutePath.EndsWith(
                "/FeatureServer/queryContingentValues",
                StringComparison.OrdinalIgnoreCase));

        var query = ParseQuery(requestUri);

        Assert.Equal("json", query["f"]);
        Assert.Equal("[2]", query["layers"]);
        Assert.Equal("false", query["compactFormat"]);
        Assert.Equal("trimmed", query["domainDictionaries"]);
    }

    private static FeatureServiceClient CreateClient(Func<HttpRequestMessage, HttpResponseMessage> handler) {
        return new FeatureServiceClient(
            new HttpClient(new StubHttpMessageHandler(handler)),
            new FeatureServiceClientOptions {
                ServiceUri = new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer")
            });
    }

    private static HttpResponseMessage CreateServiceMetadataResponse(bool supportsQueryContingentValues) {
        return StubHttpMessageHandler.Json($$"""
        {
          "layers": [
            { "id": 2, "name": "Layer 2" }
          ],
          "tables": [],
          "capabilities": "Query",
          "supportsQueryContingentValues": {{supportsQueryContingentValues.ToString().ToLowerInvariant()}}
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