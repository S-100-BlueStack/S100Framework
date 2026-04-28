using S100Framework.REST.Clients;
using S100Framework.REST.Configuration;
using S100Framework.REST.Tests.TestDoubles;
using Xunit;

namespace S100Framework.REST.Tests.Clients;

public sealed class FeatureLayerClientFieldGroupsTests
{
    [Fact]
    public async Task GetFieldGroupsAsync_MapsFieldGroups() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var requestUris = new List<Uri>();

        var client = CreateClient(request => {
            requestUris.Add(request.RequestUri!);

            if (request.RequestUri!.AbsolutePath.EndsWith(
                "/FeatureServer/0/fieldGroups",
                StringComparison.OrdinalIgnoreCase)) {
                return StubHttpMessageHandler.Json("""
                {
                  "fieldGroups": [
                    {
                      "name": "make_rating",
                      "restrictive": true,
                      "fields": [
                        "vmake",
                        "vrating"
                      ]
                    },
                    {
                      "name": "make_model",
                      "restrictive": false,
                      "fields": [
                        "vmake",
                        "vmodel"
                      ]
                    }
                  ]
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var result = await client.GetLayerClient(0).GetFieldGroupsAsync(cancellationToken);

        Assert.Equal(0, result.LayerId);
        Assert.Equal(2, result.FieldGroups.Count);

        Assert.Equal("make_rating", result.FieldGroups[0].Name);
        Assert.True(result.FieldGroups[0].Restrictive);
        Assert.Equal(["vmake", "vrating"], result.FieldGroups[0].Fields);

        Assert.Equal("make_model", result.FieldGroups[1].Name);
        Assert.False(result.FieldGroups[1].Restrictive);
        Assert.Equal(["vmake", "vmodel"], result.FieldGroups[1].Fields);

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