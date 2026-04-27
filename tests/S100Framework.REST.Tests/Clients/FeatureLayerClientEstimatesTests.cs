using S100Framework.REST.Clients;
using S100Framework.REST.Configuration;
using S100Framework.REST.Tests.TestDoubles;
using Xunit;

namespace S100Framework.REST.Tests.Clients;

public sealed class FeatureLayerClientEstimatesTests
{
    [Fact]
    public async Task GetEstimatesAsync_GetsLayerEstimates() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var requestUris = new List<Uri>();

        var client = CreateClient(request => {
            requestUris.Add(request.RequestUri!);

            if (request.RequestUri!.AbsolutePath.EndsWith(
                "/FeatureServer/0/getEstimates",
                StringComparison.OrdinalIgnoreCase)) {
                return StubHttpMessageHandler.Json("""
                {
                  "count": 931,
                  "extent": {
                    "xmin": 10,
                    "ymin": 20,
                    "xmax": 30,
                    "ymax": 40,
                    "spatialReference": {
                      "wkid": 4326,
                      "latestWkid": 4326
                    }
                  }
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var layerClient = client.GetLayerClient(0);

        var estimate = await layerClient.GetEstimatesAsync(cancellationToken);

        Assert.Equal(0, estimate.LayerId);
        Assert.Equal(931, estimate.Count);
        Assert.NotNull(estimate.Extent);
        Assert.Equal(4326, estimate.Extent!.Srid);
        Assert.Equal(10, estimate.Extent.Envelope.MinX);
        Assert.Equal(20, estimate.Extent.Envelope.MinY);
        Assert.Equal(30, estimate.Extent.Envelope.MaxX);
        Assert.Equal(40, estimate.Extent.Envelope.MaxY);

        var requestUri = Assert.Single(requestUris);
        Assert.Equal("json", ParseQuery(requestUri)["f"]);
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