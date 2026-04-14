namespace S100Framework.REST.Models;

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
    bool SupportsDistinct);