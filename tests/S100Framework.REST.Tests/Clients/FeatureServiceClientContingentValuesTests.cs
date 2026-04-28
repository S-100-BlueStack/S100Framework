using S100Framework.REST.Clients;
using S100Framework.REST.Configuration;
using S100Framework.REST.Exceptions;
using S100Framework.REST.Models;
using S100Framework.REST.Tests.TestDoubles;
using Xunit;

namespace S100Framework.REST.Tests.Clients;

public sealed class FeatureServiceClientContingentValuesTests
{
    [Fact]
    public async Task QueryContingentValuesAsync_SendsLayerIdsAndOptions() {
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
                      "id": 0,
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

        var result = await client.QueryContingentValuesAsync(
            new QueryContingentValuesRequest {
                LayerIds = [0, 2],
                CompactFormat = true,
                DomainDictionaries = QueryContingentValuesDomainDictionaries.Complete
            },
            cancellationToken);

        Assert.True(result.Payload.TryGetProperty("layers", out _));

        var requestUri = Assert.Single(
            requestUris,
            uri => uri.AbsolutePath.EndsWith(
                "/FeatureServer/queryContingentValues",
                StringComparison.OrdinalIgnoreCase));

        var query = ParseQuery(requestUri);

        Assert.Equal("json", query["f"]);
        Assert.Equal("[0,2]", query["layers"]);
        Assert.Equal("true", query["compactFormat"]);
        Assert.Equal("complete", query["domainDictionaries"]);
    }

    [Fact]
    public async Task QueryContingentValuesAsync_Throws_WhenServiceDoesNotAdvertiseSupport() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (request.RequestUri!.AbsolutePath.EndsWith(
                "/FeatureServer",
                StringComparison.OrdinalIgnoreCase)) {
                return CreateServiceMetadataResponse(supportsQueryContingentValues: false);
            }

            throw new InvalidOperationException("HTTP should not be called after metadata lookup.");
        });

        var exception = await Assert.ThrowsAsync<FeatureServiceCapabilityException>(() =>
            client.QueryContingentValuesAsync(
                new QueryContingentValuesRequest {
                    LayerIds = [0]
                },
                cancellationToken));

        Assert.Contains("queryContingentValues", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task QueryContingentValuesAsync_Throws_WhenLayerIdsContainDuplicates() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(_ =>
            throw new InvalidOperationException("HTTP should not be called."));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.QueryContingentValuesAsync(
                new QueryContingentValuesRequest {
                    LayerIds = [0, 0]
                },
                cancellationToken));

        Assert.Contains("duplicate", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetMetadataAsync_MapsQueryContingentValuesCapability() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (request.RequestUri!.AbsolutePath.EndsWith(
                "/FeatureServer",
                StringComparison.OrdinalIgnoreCase)) {
                return CreateServiceMetadataResponse(supportsQueryContingentValues: true);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var metadata = await client.GetMetadataAsync(cancellationToken);

        Assert.True(metadata.Capabilities.SupportsQueryContingentValues);
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
            { "id": 0, "name": "Layer 0" },
            { "id": 2, "name": "Layer 2" }
          ],
          "tables": [],
          "capabilities": "Query",
          "supportsQueryContingentValues": {{supportsQueryContingentValues.ToString().ToLowerInvariant()}},
          "supportedContingentValuesFormats": "JSON, PBF",
          "supportsContingentValuesJson": 2
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