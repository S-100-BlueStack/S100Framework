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

            if (IsFieldGroupsRequest(request)) {
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

    [Fact]
    public async Task GetFieldGroupsAsync_ReturnsEmptyList_WhenFieldGroupsPropertyIsMissing() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsFieldGroupsRequest(request)) {
                return StubHttpMessageHandler.Json("{}");
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var result = await client.GetLayerClient(0).GetFieldGroupsAsync(cancellationToken);

        Assert.Equal(0, result.LayerId);
        Assert.Empty(result.FieldGroups);
    }

    [Fact]
    public async Task GetFieldGroupsAsync_ReturnsEmptyList_WhenFieldGroupsPropertyIsNull() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsFieldGroupsRequest(request)) {
                return StubHttpMessageHandler.Json("""
                {
                  "fieldGroups": null
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var result = await client.GetLayerClient(0).GetFieldGroupsAsync(cancellationToken);

        Assert.Equal(0, result.LayerId);
        Assert.Empty(result.FieldGroups);
    }

    [Fact]
    public async Task GetFieldGroupsAsync_IgnoresNullFieldGroupItems() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsFieldGroupsRequest(request)) {
                return StubHttpMessageHandler.Json("""
                {
                  "fieldGroups": [
                    null,
                    {
                      "name": "make_rating",
                      "restrictive": true,
                      "fields": [
                        "vmake",
                        "vrating"
                      ]
                    }
                  ]
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var result = await client.GetLayerClient(0).GetFieldGroupsAsync(cancellationToken);

        var fieldGroup = Assert.Single(result.FieldGroups);

        Assert.Equal("make_rating", fieldGroup.Name);
        Assert.True(fieldGroup.Restrictive);
        Assert.Equal(["vmake", "vrating"], fieldGroup.Fields);
    }

    [Fact]
    public async Task GetFieldGroupsAsync_FiltersMissingAndBlankFields() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsFieldGroupsRequest(request)) {
                return StubHttpMessageHandler.Json("""
                {
                  "fieldGroups": [
                    {
                      "name": "make_rating",
                      "fields": [
                        null,
                        "",
                        "   ",
                        "vmake",
                        "vrating"
                      ]
                    }
                  ]
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var result = await client.GetLayerClient(0).GetFieldGroupsAsync(cancellationToken);

        var fieldGroup = Assert.Single(result.FieldGroups);

        Assert.Equal("make_rating", fieldGroup.Name);
        Assert.Null(fieldGroup.Restrictive);
        Assert.Equal(["vmake", "vrating"], fieldGroup.Fields);
    }

    [Fact]
    public async Task GetFieldGroupsAsync_ReturnsEmptyFields_WhenFieldsPropertyIsMissing() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsFieldGroupsRequest(request)) {
                return StubHttpMessageHandler.Json("""
                {
                  "fieldGroups": [
                    {
                      "name": "make_rating",
                      "restrictive": true
                    }
                  ]
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var result = await client.GetLayerClient(0).GetFieldGroupsAsync(cancellationToken);

        var fieldGroup = Assert.Single(result.FieldGroups);

        Assert.Equal("make_rating", fieldGroup.Name);
        Assert.True(fieldGroup.Restrictive);
        Assert.Empty(fieldGroup.Fields);
    }

    private static FeatureServiceClient CreateClient(Func<HttpRequestMessage, HttpResponseMessage> handler) {
        return new FeatureServiceClient(
            new HttpClient(new StubHttpMessageHandler(handler)),
            new FeatureServiceClientOptions {
                ServiceUri = new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer")
            });
    }

    private static bool IsFieldGroupsRequest(HttpRequestMessage request) {
        return request.RequestUri?.AbsolutePath.EndsWith(
            "/FeatureServer/0/fieldGroups",
            StringComparison.OrdinalIgnoreCase) == true;
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