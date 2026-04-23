using S100Framework.REST.Abstractions;
using S100Framework.REST.Extensions;
using S100Framework.REST.Models;
using Xunit;

namespace S100Framework.REST.Tests.Extensions;

public sealed class FeatureServiceClientExtractChangesExtensionsTimeoutTests
{
    [Fact]
    public async Task WaitForExtractChangesCompletionAsync_ThrowsTimeoutException_WhenJobDoesNotComplete() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = new NeverCompletingFeatureServiceClient();

        var exception = await Assert.ThrowsAsync<TimeoutException>(() =>
            client.WaitForExtractChangesCompletionAsync(
                new Uri("https://example.test/jobs/extractChanges-123/status"),
                new ExtractChangesPollingOptions {
                    PollInterval = TimeSpan.FromMilliseconds(5),
                    Timeout = TimeSpan.FromMilliseconds(25)
                },
                cancellationToken));

        Assert.Contains("did not complete within", exception.Message);
        Assert.True(client.StatusRequestCount >= 1);
    }

    [Fact]
    public async Task SubmitAndDownloadExtractChangesFileAsync_ThrowsTimeoutException_WhenJobDoesNotComplete() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = new NeverCompletingFeatureServiceClient();

        var exception = await Assert.ThrowsAsync<TimeoutException>(() =>
            client.SubmitAndDownloadExtractChangesFileAsync(
                new ExtractChangesRequest {
                    Layers = [0],
                    LayerServerGens = [new ExtractChangesLayerServerGen(0, 1653608093000)],
                    ReturnInserts = true,
                    ReturnUpdates = true,
                    ReturnDeletes = true,
                    DataFormat = ExtractChangesDataFormat.Sqlite
                },
                new ExtractChangesPollingOptions {
                    PollInterval = TimeSpan.FromMilliseconds(5),
                    Timeout = TimeSpan.FromMilliseconds(25)
                },
                cancellationToken));

        Assert.Contains("did not complete within", exception.Message);
        Assert.True(client.StatusRequestCount >= 1);
        Assert.False(client.DownloadWasCalled);
    }

    private sealed class NeverCompletingFeatureServiceClient : IFeatureServiceClient
    {
        public int StatusRequestCount { get; private set; }

        public bool DownloadWasCalled { get; private set; }

        public Task<FeatureServiceMetadata> GetMetadataAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public IFeatureLayerClient GetLayerClient(int layerId) =>
            throw new NotSupportedException();

        public Task<IFeatureLayerClient> GetLayerClientAsync(
            string layerName,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<FeatureServiceApplyEditsResult> ApplyEditsAsync(
            FeatureServiceEdits edits,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<FeatureServiceApplyEditsSubmissionResult> SubmitApplyEditsAsync(
            FeatureServiceEdits edits,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<ApplyEditsJobStatus> GetApplyEditsStatusAsync(
            Uri statusUrl,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<FeatureServiceApplyEditsResult> GetApplyEditsResultAsync(
            Uri resultUrl,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<FeatureServiceApplyEditsResult> WaitForApplyEditsCompletionAsync(
            FeatureServiceEdits edits,
            ApplyEditsWaitOptions? options = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<FeatureServiceApplyEditsResult> WaitForApplyEditsCompletionAsync(
            Uri statusUrl,
            ApplyEditsWaitOptions? options = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<ExtractChangesResult> ExtractChangesAsync(
            ExtractChangesRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<ExtractChangesSubmissionResult> SubmitExtractChangesAsync(
            ExtractChangesRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(
                new ExtractChangesSubmissionResult(
                    Result: null,
                    StatusUrl: new Uri("https://example.test/jobs/extractChanges-123/status")));

        public Task<ExtractChangesJobStatus> GetExtractChangesStatusAsync(
            Uri statusUrl,
            CancellationToken cancellationToken = default) {
            StatusRequestCount++;

            return Task.FromResult(
                new ExtractChangesJobStatus(
                    Status: "Executing",
                    ResponseType: null,
                    TransportType: null,
                    ResultUrl: null,
                    SubmissionTime: null,
                    LastUpdatedTime: null));
        }

        public Task<ExtractChangesFileResult> DownloadExtractChangesFileAsync(
            Uri resultUrl,
            CancellationToken cancellationToken = default) {
            DownloadWasCalled = true;
            throw new NotSupportedException("Download should not be reached in timeout tests.");
        }

        Task<FeatureServiceAppendSubmissionResult> IFeatureServiceClient.SubmitAppendAsync(
    FeatureServiceAppendEditsRequest request,
    CancellationToken cancellationToken) {
            throw new NotSupportedException("Append operations are not used by this test double.");
        }

        Task<FeatureServiceAppendJobStatus> IFeatureServiceClient.GetAppendStatusAsync(
            Uri statusUrl,
            CancellationToken cancellationToken) {
            throw new NotSupportedException("Append operations are not used by this test double.");
        }

        Task<FeatureServiceAppendJobStatus> IFeatureServiceClient.WaitForAppendCompletionAsync(
            FeatureServiceAppendEditsRequest request,
            AppendWaitOptions? options,
            CancellationToken cancellationToken) {
            throw new NotSupportedException("Append operations are not used by this test double.");
        }

        Task<FeatureServiceAppendJobStatus> IFeatureServiceClient.WaitForAppendCompletionAsync(
            Uri statusUrl,
            AppendWaitOptions? options,
            CancellationToken cancellationToken) {
            throw new NotSupportedException("Append operations are not used by this test double.");
        }
    }
}