using S100Framework.REST.Clients;
using S100Framework.REST.Configuration;
using S100Framework.REST.Models;
using S100Framework.REST.Tests.TestDoubles;
using Xunit;

namespace S100Framework.REST.Tests.Clients;

public sealed class FeatureServiceClientContingentValuesHardeningTests
{
    [Fact]
    public async Task GetMetadataAsync_MapsContingentValuesFormatMetadata() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsServiceMetadataRequest(request)) {
                return CreateServiceMetadataResponse(
                    supportedContingentValuesFormats: "JSON, PBF",
                    supportsContingentValuesJson: 2);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var metadata = await client.GetMetadataAsync(cancellationToken);

        Assert.Equal(
            ["JSON", "PBF"],
            metadata.Capabilities.SupportedContingentValuesFormats);

        Assert.Equal(2, metadata.Capabilities.ContingentValuesJsonVersion);
        Assert.True(metadata.Capabilities.SupportsContingentValuesJson);
    }

    [Fact]
    public async Task GetMetadataAsync_IgnoresBlankAndDuplicateContingentValuesFormats() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsServiceMetadataRequest(request)) {
                return CreateServiceMetadataResponse(
                    supportedContingentValuesFormats: " JSON, , PBF, json ",
                    supportsContingentValuesJson: null);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var metadata = await client.GetMetadataAsync(cancellationToken);

        Assert.Equal(
            ["JSON", "PBF"],
            metadata.Capabilities.SupportedContingentValuesFormats);

        Assert.Null(metadata.Capabilities.ContingentValuesJsonVersion);
        Assert.False(metadata.Capabilities.SupportsContingentValuesJson);
    }

    [Fact]
    public async Task QueryContingentValuesAsync_Throws_WhenDomainDictionariesIsInvalid() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(_ =>
            throw new InvalidOperationException("HTTP should not be called."));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.QueryContingentValuesAsync(
                new QueryContingentValuesRequest {
                    DomainDictionaries = (QueryContingentValuesDomainDictionaries)999
                },
                cancellationToken));

        Assert.Contains("DomainDictionaries", exception.Message, StringComparison.Ordinal);
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
        string? supportedContingentValuesFormats,
        int? supportsContingentValuesJson) {
        var supportedContingentValuesFormatsJson = supportedContingentValuesFormats is null
            ? "null"
            : $"\"{supportedContingentValuesFormats}\"";

        var supportsContingentValuesJsonValue = supportsContingentValuesJson?.ToString(
            System.Globalization.CultureInfo.InvariantCulture) ?? "null";

        return StubHttpMessageHandler.Json($$"""
        {
          "layers": [
            { "id": 0, "name": "Layer 0" }
          ],
          "tables": [],
          "capabilities": "Query",
          "supportsQueryContingentValues": true,
          "supportedContingentValuesFormats": {{supportedContingentValuesFormatsJson}},
          "supportsContingentValuesJson": {{supportsContingentValuesJsonValue}}
        }
        """);
    }
}