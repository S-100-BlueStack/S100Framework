using S100Framework.REST.Abstractions;
using S100Framework.REST.Extensions;
using S100Framework.REST.Models;
using Xunit;

namespace S100Framework.REST.Tests.Extensions;

public sealed class FeatureServiceClientExtractChangesExtensionsTests
{
    [Fact]
    public async Task WaitForExtractChangesCompletionAsync_ReturnsCompletedStatus() {
        var client = new FakeFeatureServiceClient(
            submission: new ExtractChangesSubmissionResult(
                Result: null,
                StatusUrl: new Uri("https://example.test/jobs/j123")),
            statuses:
            [
                new ExtractChangesJobStatus(
                    Status: "Executing",
                    ResponseType: null,
                    TransportType: null,
                    ResultUrl: null,
                    SubmissionTime: null,
                    LastUpdatedTime: null),
                new ExtractChangesJobStatus(
                    Status: "Completed",
                    ResponseType: "esriDataChangesResponseTypeEdits",
                    TransportType: "esriTransportTypeUrl",
                    ResultUrl: new Uri("https://example.test/output/changes.sqlite"),
                    SubmissionTime: 1,
                    LastUpdatedTime: 2)
            ],
            downloadResult: new ExtractChangesFileResult(
                Content: [1, 2, 3],
                ContentType: "application/octet-stream",
                FileName: "changes.sqlite",
                ResultUrl: new Uri("https://example.test/output/changes.sqlite")));

        var status = await client.WaitForExtractChangesCompletionAsync(
            new Uri("https://example.test/jobs/j123"),
            new ExtractChangesPollingOptions {
                PollInterval = TimeSpan.FromMilliseconds(1),
                Timeout = TimeSpan.FromSeconds(1)
            });

        Assert.True(status.IsCompleted);
        Assert.Equal(2, client.StatusRequestCount);
    }

    [Fact]
    public async Task SubmitAndDownloadExtractChangesFileAsync_SubmitsPollsAndDownloadsResult() {
        var client = new FakeFeatureServiceClient(
            submission: new ExtractChangesSubmissionResult(
                Result: null,
                StatusUrl: new Uri("https://example.test/jobs/j123")),
            statuses:
            [
                new ExtractChangesJobStatus(
                    Status: "Pending",
                    ResponseType: null,
                    TransportType: null,
                    ResultUrl: null,
                    SubmissionTime: null,
                    LastUpdatedTime: null),
                new ExtractChangesJobStatus(
                    Status: "Completed",
                    ResponseType: "esriDataChangesResponseTypeEdits",
                    TransportType: "esriTransportTypeUrl",
                    ResultUrl: new Uri("https://example.test/output/changes.sqlite"),
                    SubmissionTime: 1,
                    LastUpdatedTime: 2)
            ],
            downloadResult: new ExtractChangesFileResult(
                Content: [1, 2, 3],
                ContentType: "application/octet-stream",
                FileName: "changes.sqlite",
                ResultUrl: new Uri("https://example.test/output/changes.sqlite")));

        var result = await client.SubmitAndDownloadExtractChangesFileAsync(
            new ExtractChangesRequest {
                Layers = [0],
                LayerServerGens = [new ExtractChangesLayerServerGen(0, 1653608093000)],
                DataFormat = ExtractChangesDataFormat.Sqlite
            },
            new ExtractChangesPollingOptions {
                PollInterval = TimeSpan.FromMilliseconds(1),
                Timeout = TimeSpan.FromSeconds(1)
            });

        Assert.Equal("changes.sqlite", result.FileName);
        Assert.Equal("https://example.test/output/changes.sqlite", client.DownloadedResultUrl!.AbsoluteUri);
        Assert.Equal(2, client.StatusRequestCount);
    }

    [Fact]
    public async Task SubmitAndDownloadExtractChangesFileAsync_Throws_WhenJobFails() {
        var client = new FakeFeatureServiceClient(
            submission: new ExtractChangesSubmissionResult(
                Result: null,
                StatusUrl: new Uri("https://example.test/jobs/j123")),
            statuses:
            [
                new ExtractChangesJobStatus(
                    Status: "Failed",
                    ResponseType: null,
                    TransportType: null,
                    ResultUrl: null,
                    SubmissionTime: null,
                    LastUpdatedTime: null)
            ],
            downloadResult: new ExtractChangesFileResult(
                Content: [1, 2, 3],
                ContentType: "application/octet-stream",
                FileName: "changes.sqlite",
                ResultUrl: new Uri("https://example.test/output/changes.sqlite")));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.SubmitAndDownloadExtractChangesFileAsync(
                new ExtractChangesRequest {
                    Layers = [0],
                    LayerServerGens = [new ExtractChangesLayerServerGen(0, 1653608093000)],
                    DataFormat = ExtractChangesDataFormat.Sqlite
                },
                new ExtractChangesPollingOptions {
                    PollInterval = TimeSpan.FromMilliseconds(1),
                    Timeout = TimeSpan.FromSeconds(1)
                }));

        Assert.Contains("Failed", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class FakeFeatureServiceClient : IFeatureServiceClient
    {
        private readonly Queue<ExtractChangesJobStatus> _statuses;
        private readonly ExtractChangesSubmissionResult _submission;
        private readonly ExtractChangesFileResult _downloadResult;

        public FakeFeatureServiceClient(
            ExtractChangesSubmissionResult submission,
            IReadOnlyList<ExtractChangesJobStatus> statuses,
            ExtractChangesFileResult downloadResult) {
            _submission = submission;
            _statuses = new Queue<ExtractChangesJobStatus>(statuses);
            _downloadResult = downloadResult;
        }

        public int StatusRequestCount { get; private set; }

        public Uri? DownloadedResultUrl { get; private set; }

        public Task<FeatureServiceMetadata> GetMetadataAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public IFeatureLayerClient GetLayerClient(int layerId)
            => throw new NotSupportedException();

        public Task<FeatureServiceApplyEditsResult> ApplyEditsAsync(
            FeatureServiceEdits edits,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IFeatureLayerClient> GetLayerClientAsync(
            string layerName,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<ExtractChangesResult> ExtractChangesAsync(
            ExtractChangesRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<ExtractChangesSubmissionResult> SubmitExtractChangesAsync(
            ExtractChangesRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(_submission);

        public Task<ExtractChangesJobStatus> GetExtractChangesStatusAsync(
            Uri statusUrl,
            CancellationToken cancellationToken = default) {
            StatusRequestCount++;

            if (_statuses.Count == 0) {
                throw new InvalidOperationException("No more statuses were configured.");
            }

            return Task.FromResult(_statuses.Dequeue());
        }

        public Task<ExtractChangesFileResult> DownloadExtractChangesFileAsync(
            Uri resultUrl,
            CancellationToken cancellationToken = default) {
            DownloadedResultUrl = resultUrl;
            return Task.FromResult(_downloadResult);
        }
    }
}