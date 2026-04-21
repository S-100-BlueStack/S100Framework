using S100Framework.REST.Clients;
using S100Framework.REST.Configuration;
using S100Framework.REST.Models;
using S100Framework.REST.Tests.TestDoubles;
using System.Net.Http;
using Xunit;

namespace S100Framework.REST.Tests.Clients;

public sealed class ApplyEditsWaitOptionsValidationTests
{
    [Fact]
    public async Task ServiceWaitForApplyEditsCompletionAsync_Throws_WhenPollIntervalIsNegative() {
        var client = new FeatureServiceClient(
            new HttpClient(new StubHttpMessageHandler(_ =>
                throw new InvalidOperationException("The HTTP request should not be executed."))),
            new FeatureServiceClientOptions {
                ServiceUri = new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer")
            });

        var exception = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            client.WaitForApplyEditsCompletionAsync(
                new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer/jobs/applyEdits-123/status"),
                new ApplyEditsWaitOptions {
                    PollInterval = TimeSpan.FromMilliseconds(-1)
                }));

        Assert.Equal(nameof(ApplyEditsWaitOptions.PollInterval), exception.ParamName);
    }

    [Fact]
    public async Task LayerWaitForApplyEditsCompletionAsync_Throws_WhenTimeoutIsNegative() {
        var layerClient = new FeatureServiceClient(
            new HttpClient(new StubHttpMessageHandler(_ =>
                throw new InvalidOperationException("The HTTP request should not be executed."))),
            new FeatureServiceClientOptions {
                ServiceUri = new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer")
            }).GetLayerClient(0);

        var exception = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            layerClient.WaitForApplyEditsCompletionAsync(
                new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer/jobs/applyEdits-123/status"),
                new ApplyEditsWaitOptions {
                    Timeout = TimeSpan.FromMilliseconds(-1)
                }));

        Assert.Equal(nameof(ApplyEditsWaitOptions.Timeout), exception.ParamName);
    }
}