using S100Framework.REST.Models;

namespace S100Framework.REST.Abstractions;

public interface IFeatureLayerClient
{
    Task<FeatureLayerSchema> GetSchemaAsync(CancellationToken cancellationToken = default);

    IAsyncEnumerable<FeatureRecord> QueryAsync(
        FeatureQuery query,
        CancellationToken cancellationToken = default);

    Task<long> QueryCountAsync(
        FeatureQuery query,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<long>> QueryObjectIdsAsync(
        FeatureQuery query,
        CancellationToken cancellationToken = default);

    Task<FeatureExtent?> QueryExtentAsync(
        FeatureQuery query,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StatisticRow>> QueryStatisticsAsync(
        FeatureStatisticsQuery query,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RelatedRecordGroup>> QueryRelatedRecordsAsync(
        RelatedRecordsQuery query,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AttachmentGroup>> QueryAttachmentsAsync(
        AttachmentQuery query,
        CancellationToken cancellationToken = default);

    Task<AttachmentContent> DownloadAttachmentAsync(
        long objectId,
        long attachmentId,
        CancellationToken cancellationToken = default);
}