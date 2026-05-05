using S100Framework.REST.Clients;
using S100Framework.REST.Configuration;
using S100Framework.REST.Models;
using S100Framework.REST.Tests.TestDoubles;
using Xunit;

namespace S100Framework.REST.Tests.Clients;

public sealed class FeatureServiceClientAppendSourceFormatHardeningTests
{
    [Fact]
    public async Task ServiceAppendItemAsync_Throws_WhenAppendUploadFormatIsInvalid() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = CreateClient(_ =>
            throw new InvalidOperationException("HTTP should not be called."));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.SubmitAppendAsync(
                new FeatureServiceAppendItemRequest {
                    Layers = [0],
                    AppendItemId = "portal-item-1",
                    AppendUploadFormat = (FeatureServiceAppendSourceFormat)999
                },
                cancellationToken));

        Assert.Contains("AppendUploadFormat", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ServiceAppendUploadAsync_Throws_WhenAppendUploadFormatIsInvalid() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = CreateClient(_ =>
            throw new InvalidOperationException("HTTP should not be called."));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.SubmitAppendAsync(
                new FeatureServiceAppendUploadRequest {
                    Layers = [0],
                    AppendUploadId = "0c6b928f590f49ebac04761bab413e49",
                    AppendUploadFormat = (FeatureServiceAppendSourceFormat)999
                },
                cancellationToken));

        Assert.Contains("AppendUploadFormat", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LayerAppendItemAsync_Throws_WhenAppendUploadFormatIsInvalid() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var layerClient = CreateClient(_ =>
            throw new InvalidOperationException("HTTP should not be called."))
            .GetLayerClient(0);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            layerClient.SubmitAppendAsync(
                new FeatureLayerAppendItemRequest {
                    AppendItemId = "portal-item-1",
                    AppendUploadFormat = (FeatureServiceAppendSourceFormat)999
                },
                cancellationToken));

        Assert.Contains("AppendUploadFormat", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LayerAppendUploadAsync_Throws_WhenAppendUploadFormatIsInvalid() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var layerClient = CreateClient(_ =>
            throw new InvalidOperationException("HTTP should not be called."))
            .GetLayerClient(0);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            layerClient.SubmitAppendAsync(
                new FeatureLayerAppendUploadRequest {
                    AppendUploadId = "0c6b928f590f49ebac04761bab413e49",
                    AppendUploadFormat = (FeatureServiceAppendSourceFormat)999
                },
                cancellationToken));

        Assert.Contains("AppendUploadFormat", exception.Message, StringComparison.Ordinal);
    }

    private static FeatureServiceClient CreateClient(Func<HttpRequestMessage, HttpResponseMessage> handler) {
        return new FeatureServiceClient(
            new HttpClient(new StubHttpMessageHandler(handler)),
            new FeatureServiceClientOptions {
                ServiceUri = new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer")
            });
    }
}