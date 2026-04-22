using S100Framework.REST.Abstractions;
using S100Framework.REST.Extensions;
using S100Framework.REST.Models;
using Xunit;

namespace S100Framework.REST.Tests.Extensions;

public sealed class FeatureLayerClientApplyEditsExtensionsTests
{
    [Fact]
    public async Task WaitForApplyEditsCompletionAsync_ReturnsCompletedStatus() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = new FakeFeatureLayerClient(
            submission: new ApplyEditsSubmissionResult(
                Result: null,
                StatusUrl: new Uri("https://example.test/jobs/apply-edits-1/status")),
            statuses:
            [
                new ApplyEditsJobStatus(
                    Status: "IN_PROGRESS",
                    ResultUrl: null,
                    SubmissionTime: 1,
                    LastUpdatedTime: 2),
                new ApplyEditsJobStatus(
                    Status: "COMPLETED",
                    ResultUrl: new Uri("https://example.test/results/apply-edits-1.json"),
                    SubmissionTime: 1,
                    LastUpdatedTime: 3)
            ],
            result: new ApplyEditsResult(
                AddResults: [new EditResult(true, 101, null, null, null)],
                UpdateResults: Array.Empty<EditResult>(),
                DeleteResults: Array.Empty<EditResult>()));

        var status = await client.WaitForApplyEditsCompletionAsync(
            new Uri("https://example.test/jobs/apply-edits-1/status"),
            new ApplyEditsPollingOptions {
                PollInterval = TimeSpan.FromMilliseconds(1),
                Timeout = TimeSpan.FromSeconds(1)
            },
            cancellationToken);

        Assert.True(status.IsCompleted);
        Assert.Equal(2, client.StatusRequestCount);
    }

    [Fact]
    public async Task SubmitAndWaitForApplyEditsAsync_ReturnsEmbeddedResult_WhenSubmissionIsSynchronous() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var embeddedResult = new ApplyEditsResult(
            AddResults: [new EditResult(true, 101, null, null, null)],
            UpdateResults: Array.Empty<EditResult>(),
            DeleteResults: Array.Empty<EditResult>());

        var client = new FakeFeatureLayerClient(
            submission: new ApplyEditsSubmissionResult(
                Result: embeddedResult,
                StatusUrl: null),
            statuses: Array.Empty<ApplyEditsJobStatus>(),
            result: embeddedResult);

        var result = await client.SubmitAndWaitForApplyEditsAsync(
            new FeatureEdits { Deletes = [1] },
            cancellationToken: cancellationToken);

        Assert.Same(embeddedResult, result);
        Assert.Equal(0, client.StatusRequestCount);
        Assert.Equal(0, client.ResultRequestCount);
    }

    [Fact]
    public async Task SubmitAndWaitForApplyEditsAsync_SubmitsPollsAndFetchesResult() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = new FakeFeatureLayerClient(
            submission: new ApplyEditsSubmissionResult(
                Result: null,
                StatusUrl: new Uri("https://example.test/jobs/apply-edits-1/status")),
            statuses:
            [
                new ApplyEditsJobStatus(
                    Status: "SUBMITTED",
                    ResultUrl: null,
                    SubmissionTime: 1,
                    LastUpdatedTime: 2),
                new ApplyEditsJobStatus(
                    Status: "COMPLETED",
                    ResultUrl: new Uri("https://example.test/results/apply-edits-1.json"),
                    SubmissionTime: 1,
                    LastUpdatedTime: 3)
            ],
            result: new ApplyEditsResult(
                AddResults: [new EditResult(true, 101, null, null, null)],
                UpdateResults: Array.Empty<EditResult>(),
                DeleteResults: Array.Empty<EditResult>()));

        var result = await client.SubmitAndWaitForApplyEditsAsync(
            new FeatureEdits { Deletes = [1] },
            new ApplyEditsPollingOptions {
                PollInterval = TimeSpan.FromMilliseconds(1),
                Timeout = TimeSpan.FromSeconds(1)
            },
            cancellationToken);

        Assert.Single(result.AddResults);
        Assert.Equal(101, result.AddResults[0].ObjectId);
        Assert.Equal(2, client.StatusRequestCount);
        Assert.Equal(1, client.ResultRequestCount);
        Assert.Equal(
            "https://example.test/results/apply-edits-1.json",
            client.ResultUrlRequested!.AbsoluteUri);
    }

    [Fact]
    public async Task SubmitAndWaitForApplyEditsAsync_Throws_WhenJobFails() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = new FakeFeatureLayerClient(
            submission: new ApplyEditsSubmissionResult(
                Result: null,
                StatusUrl: new Uri("https://example.test/jobs/apply-edits-1/status")),
            statuses:
            [
                new ApplyEditsJobStatus(
                    Status: "FAILED",
                    ResultUrl: null,
                    SubmissionTime: 1,
                    LastUpdatedTime: 2)
            ],
            result: new ApplyEditsResult(
                AddResults: Array.Empty<EditResult>(),
                UpdateResults: Array.Empty<EditResult>(),
                DeleteResults: Array.Empty<EditResult>()));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.SubmitAndWaitForApplyEditsAsync(
                new FeatureEdits { Deletes = [1] },
                new ApplyEditsPollingOptions {
                    PollInterval = TimeSpan.FromMilliseconds(1),
                    Timeout = TimeSpan.FromSeconds(1)
                },
                cancellationToken));

        Assert.Contains("FAILED", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class FakeFeatureLayerClient : IFeatureLayerClient
    {
        private readonly Queue<ApplyEditsJobStatus> _statuses;
        private readonly ApplyEditsSubmissionResult _submission;
        private readonly ApplyEditsResult _result;

        public FakeFeatureLayerClient(
            ApplyEditsSubmissionResult submission,
            IReadOnlyList<ApplyEditsJobStatus> statuses,
            ApplyEditsResult result) {
            _submission = submission;
            _statuses = new Queue<ApplyEditsJobStatus>(statuses);
            _result = result;
        }

        public int StatusRequestCount { get; private set; }

        public int ResultRequestCount { get; private set; }

        public Uri? ResultUrlRequested { get; private set; }

        public Task<FeatureLayerSchema> GetSchemaAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public IAsyncEnumerable<FeatureRecord> QueryAsync(
            FeatureQuery query,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<long> QueryCountAsync(
            FeatureQuery query,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<long>> QueryObjectIdsAsync(
            FeatureQuery query,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<FeatureExtent?> QueryExtentAsync(
            FeatureQuery query,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<StatisticRow>> QueryStatisticsAsync(
            FeatureStatisticsQuery query,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<RelatedRecordGroup>> QueryRelatedRecordsAsync(
            RelatedRecordsQuery query,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<AttachmentGroup>> QueryAttachmentsAsync(
            AttachmentQuery query,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<AttachmentContent> DownloadAttachmentAsync(
            long objectId,
            long attachmentId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<FeatureRecord>> QueryTopFeaturesAsync(
            TopFeaturesQuery query,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<long>> QueryTopFeatureObjectIdsAsync(
            TopFeaturesQuery query,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<TopFeaturesCountResult> QueryTopFeatureCountAsync(
            TopFeaturesQuery query,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<ApplyEditsResult> ApplyEditsAsync(
            FeatureEdits edits,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<ApplyEditsSubmissionResult> SubmitApplyEditsAsync(
            FeatureEdits edits,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_submission);

        public Task<ApplyEditsJobStatus> GetApplyEditsStatusAsync(
            Uri statusUrl,
            CancellationToken cancellationToken = default) {
            StatusRequestCount++;

            if (_statuses.Count == 0) {
                throw new InvalidOperationException("No more statuses were configured.");
            }

            return Task.FromResult(_statuses.Dequeue());
        }

        public Task<ApplyEditsResult> GetApplyEditsResultAsync(
            Uri resultUrl,
            CancellationToken cancellationToken = default) {
            ResultRequestCount++;
            ResultUrlRequested = resultUrl;

            return Task.FromResult(_result);
        }

        public Task<DeleteAttachmentsResult> DeleteAttachmentsAsync(
            DeleteAttachmentsRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<AddAttachmentResult> AddAttachmentAsync(
            AddAttachmentRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<UpdateAttachmentResult> UpdateAttachmentAsync(
            UpdateAttachmentRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        Task<ApplyEditsResult> IFeatureLayerClient.WaitForApplyEditsCompletionAsync(
            FeatureEdits edits,
            ApplyEditsWaitOptions? options,
            CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        Task<ApplyEditsResult> IFeatureLayerClient.WaitForApplyEditsCompletionAsync(
            Uri statusUrl,
            ApplyEditsWaitOptions? options,
            CancellationToken cancellationToken) =>
            throw new NotImplementedException();
    }
}