namespace S100Framework.REST.Models;

/// <summary>
/// Describes the core capabilities exposed by a layer or table.
/// </summary>
/// <param name="HasAttachments">Indicates whether the layer supports attachments.</param>
/// <param name="SupportsQueryAttachments">Indicates whether attachment query operations are supported.</param>
/// <param name="SupportsAttachmentsResizing">Indicates whether attachment resizing is supported.</param>
/// <param name="SupportsTopFeaturesQuery">Indicates whether top-features queries are supported.</param>
/// <param name="SupportsPagination">Indicates whether standard pagination is supported.</param>
/// <param name="SupportsPaginationOnAggregatedQueries">Indicates whether pagination is supported for aggregated queries.</param>
/// <param name="SupportsQueryRelatedPagination">Indicates whether pagination is supported for related-record queries.</param>
/// <param name="SupportsAdvancedQueryRelated">Indicates whether advanced related-record query functionality is supported.</param>
/// <param name="SupportsOrderBy">Indicates whether order-by clauses are supported.</param>
/// <param name="SupportsDistinct">Indicates whether distinct queries are supported.</param>
/// <param name="SupportsAsyncApplyEdits">Indicates whether asynchronous layer-level <c>applyEdits</c> is supported.</param>
/// <param name="SupportsReturningGeometryEnvelope">Indicates whether the layer supports returning geometry envelopes from query operations.</param>
/// <param name="SupportsFullTextSearch">Indicates whether the layer supports full-text search.</param>
/// <param name="SupportsPercentileStatistics">Indicates whether the layer supports percentile statistics.</param>
/// <param name="SupportsAppend">Indicates whether layer-level append is supported.</param>
public sealed record FeatureLayerCapabilities(
    bool HasAttachments,
    bool SupportsQueryAttachments,
    bool SupportsAttachmentsResizing,
    bool SupportsTopFeaturesQuery,
    bool SupportsPagination,
    bool SupportsPaginationOnAggregatedQueries,
    bool SupportsQueryRelatedPagination,
    bool SupportsAdvancedQueryRelated,
    bool SupportsOrderBy,
    bool SupportsDistinct,
    bool SupportsAsyncApplyEdits,
    bool SupportsReturningGeometryEnvelope = false,
    bool SupportsFullTextSearch = false,
    bool SupportsPercentileStatistics = false,
    bool SupportsAppend = false);