using S100Framework.REST.Clients;
using S100Framework.REST.Configuration;
using S100Framework.REST.Tests.TestDoubles;
using Xunit;

namespace S100Framework.REST.Tests.Clients;

public sealed class FeatureServiceClientEstimatesTests
{
    [Fact]
    public async Task GetEstimatesAsync_GetsServiceEstimates() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var requestUris = new List<Uri>();

        var client = CreateClient(request => {
            requestUris.Add(request.RequestUri!);

            if (request.RequestUri!.AbsolutePath.EndsWith(
                "/FeatureServer/getEstimates",
                StringComparison.OrdinalIgnoreCase)) {
                return StubHttpMessageHandler.Json("""
                {
                  "layerEstimates": [
                    {
                      "layerId": 0,
                      "count": 48,
                      "extent": {
                        "xmin": 1,
                        "ymin": 2,
                        "xmax": 3,
                        "ymax": 4,
                        "spatialReference": {
                          "wkid": 102100,
                          "latestWkid": 3857
                        }
                      }
                    },
                    {
                      "layerId": 1,
                      "count": 69
                    }
                  ]
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var estimates = await client.GetEstimatesAsync(cancellationToken);

        Assert.Equal(2, estimates.Count);

        Assert.Equal(0, estimates[0].LayerId);
        Assert.Equal(48, estimates[0].Count);
        Assert.NotNull(estimates[0].Extent);
        Assert.Equal(3857, estimates[0].Extent!.Srid);

        Assert.Equal(1, estimates[1].LayerId);
        Assert.Equal(69, estimates[1].Count);
        Assert.Null(estimates[1].Extent);

        var requestUri = Assert.Single(requestUris);
        Assert.Equal("json", ParseQuery(requestUri)["f"]);
    }

    [Fact]
    public async Task GetEstimatesAsync_IncludesLayerIds_WhenProvided() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var requestUris = new List<Uri>();

        var client = CreateClient(request => {
            requestUris.Add(request.RequestUri!);

            if (request.RequestUri!.AbsolutePath.EndsWith(
                "/FeatureServer/getEstimates",
                StringComparison.OrdinalIgnoreCase)) {
                return StubHttpMessageHandler.Json("""
                {
                  "layerEstimates": [
                    {
                      "layerId": 0,
                      "count": 48
                    },
                    {
                      "layerId": 2,
                      "count": 15
                    }
                  ]
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var estimates = await client.GetEstimatesAsync([0, 2], cancellationToken);

        Assert.Equal(2, estimates.Count);
        Assert.Equal(0, estimates[0].LayerId);
        Assert.Equal(2, estimates[1].LayerId);

        var requestUri = Assert.Single(requestUris);
        var query = ParseQuery(requestUri);

        Assert.Equal("json", query["f"]);
        Assert.Equal("[0,2]", query["layers"]);
    }

    [Fact]
    public async Task GetEstimatesAsync_Throws_WhenLayerIdsContainDuplicates() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(_ =>
            throw new InvalidOperationException("HTTP should not be called."));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.GetEstimatesAsync([0, 0], cancellationToken));

        Assert.Contains("duplicate", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static FeatureServiceClient CreateClient(Func<HttpRequestMessage, HttpResponseMessage> handler) {
        return new FeatureServiceClient(
            new HttpClient(new StubHttpMessageHandler(handler)),
            new FeatureServiceClientOptions {
                ServiceUri = new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer")
            });
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