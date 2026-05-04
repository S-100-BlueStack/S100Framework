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
/// <param name="SupportsQueryDateBins">Indicates whether the layer supports <c>queryDateBins</c>.</param>
/// <param name="SupportsQueryAnalytic">Indicates whether the layer supports <c>queryAnalytic</c>.</param>
/// <param name="SupportsAsyncQueryAnalytic">Indicates whether the layer supports asynchronous <c>queryAnalytic</c>.</param>
/// <param name="SupportsCalculate">Indicates whether the layer supports the <c>calculate</c> operation.</param>
/// <param name="SupportsAsyncCalculate">Indicates whether the layer supports asynchronous <c>calculate</c>.</param>
/// <param name="SupportsReturningQueryExtent">Indicates whether extent-only layer queries are supported.</param>
/// <param name="SupportsReturningGeometryCentroid">Indicates whether feature query centroid output is supported.</param>
/// <param name="SupportsDefaultSrid">Indicates whether the layer supports the query <c>defaultSR</c> parameter.</param>
/// <param name="SupportsOutFieldSqlExpression">Indicates whether SQL expressions are supported in query out fields.</param>
/// <param name="SupportsSqlExpression">Indicates whether SQL expressions are supported in statistics, group-by, and order-by query parameters.</param>
/// <param name="SupportsHavingClause">Indicates whether statistics queries support <c>havingClause</c>.</param>
/// <param name="SupportsQueryWithDistance">Indicates whether spatial queries support distance and units parameters.</param>
/// <param name="SupportsQueryWithResultType">Indicates whether queries support <c>resultType</c>.</param>
/// <param name="SupportsQueryWithHistoricMoment">Indicates whether queries support <c>historicMoment</c>.</param>
/// <param name="SupportsQueryWithDatumTransformation">Indicates whether queries support <c>datumTransformation</c>.</param>
/// <param name="SupportsCoordinatesQuantization">Indicates whether queries support coordinate quantization.</param>
/// <param name="SupportsCurrentUserQueries">Indicates whether WHERE clauses support the <c>current_user</c> keyword.</param>
/// <param name="SupportsQueryWithCacheHint">Indicates whether queries support cache hints.</param>
/// <param name="SupportsQueryAttachmentsCountOnly">Indicates whether attachment count-only queries are supported.</param>
/// <param name="SupportsQueryAttachmentOrderByFields">Indicates whether attachment query ordering is supported.</param>
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
    bool SupportsAppend = false,
    bool SupportsQueryDateBins = false,
    bool SupportsQueryAnalytic = false,
    bool SupportsAsyncQueryAnalytic = false,
    bool SupportsCalculate = false,
    bool SupportsAsyncCalculate = false,
    bool SupportsReturningQueryExtent = false,
    bool SupportsReturningGeometryCentroid = false,
    bool SupportsDefaultSrid = false,
    bool SupportsOutFieldSqlExpression = false,
    bool SupportsSqlExpression = false,
    bool SupportsHavingClause = false,
    bool SupportsQueryWithDistance = false,
    bool SupportsQueryWithResultType = false,
    bool SupportsQueryWithHistoricMoment = false,
    bool SupportsQueryWithDatumTransformation = false,
    bool SupportsCoordinatesQuantization = false,
    bool SupportsCurrentUserQueries = false,
    bool SupportsQueryWithCacheHint = false,
    bool SupportsQueryAttachmentsCountOnly = false,
    bool SupportsQueryAttachmentOrderByFields = false)
{
    /// <summary>
    /// Gets the SQL dialects the layer advertises for <c>calculate</c> expressions.
    /// </summary>
    /// <remarks>
    /// An empty collection means the layer did not advertise the value, so callers should rely on server-side validation.
    /// </remarks>
    public IReadOnlyList<FeatureQuerySqlFormat> SupportedSqlFormatsInCalculate { get; init; } = Array.Empty<FeatureQuerySqlFormat>();
}