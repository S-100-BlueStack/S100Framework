using S100Framework.REST.Models;

namespace S100Framework.REST.Abstractions;

/// <summary>
/// Defines service-level query operations for a feature service endpoint.
/// </summary>
public partial interface IFeatureServiceClient
{
    /// <summary>
    /// Executes a service-level <c>query</c> request against one or more layers or tables.
    /// </summary>
    /// <param name="request">
    /// The service-level query request.
    /// </param>
    /// <param name="cancellationToken">
    /// The cancellation token.
    /// </param>
    /// <returns>
    /// The layer-grouped feature records returned by the feature service.
    /// </returns>
    /// <exception cref="NotSupportedException">
    /// Thrown when the concrete service client implementation does not support service-level query operations.
    /// </exception>
    Task<FeatureServiceQueryResult> QueryAsync(
        FeatureServiceQueryRequest request,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This feature service client does not support service-level query operations.");

    /// <summary>
    /// Executes layer-level feature queries for all layer definitions in a service-level query request.
    /// </summary>
    /// <param name="request">
    /// The service-level query request used to derive the layer-level feature queries.
    /// </param>
    /// <param name="cancellationToken">
    /// The cancellation token.
    /// </param>
    /// <returns>
    /// The layer-grouped feature records returned by the feature service.
    /// </returns>
    /// <remarks>
    /// This method is intended for consumers that need complete multi-layer results. It executes one layer-level
    /// query per layer definition, allowing the existing layer query paging and object ID fallback behavior to avoid
    /// service-level feature-set transfer limits.
    /// </remarks>
    /// <exception cref="NotSupportedException">
    /// Thrown when the concrete service client implementation does not support service-level query operations.
    /// </exception>
    Task<FeatureServiceQueryResult> QueryAllAsync(
        FeatureServiceQueryRequest request,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This feature service client does not support service-level query operations.");

    /// <summary>
    /// Executes a service-level <c>query</c> request that returns row counts for one or more layers or tables.
    /// </summary>
    /// <param name="request">
    /// The service-level query request.
    /// </param>
    /// <param name="cancellationToken">
    /// The cancellation token.
    /// </param>
    /// <returns>
    /// The layer-grouped row counts returned by the feature service.
    /// </returns>
    /// <exception cref="NotSupportedException">
    /// Thrown when the concrete service client implementation does not support service-level query operations.
    /// </exception>
    Task<FeatureServiceQueryCountResult> QueryCountAsync(
        FeatureServiceQueryRequest request,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This feature service client does not support service-level query operations.");

    /// <summary>
    /// Executes a service-level <c>query</c> request that returns object IDs for one or more layers or tables.
    /// </summary>
    /// <param name="request">
    /// The service-level query request.
    /// </param>
    /// <param name="cancellationToken">
    /// The cancellation token.
    /// </param>
    /// <returns>
    /// The layer-grouped object IDs returned by the feature service.
    /// </returns>
    /// <exception cref="NotSupportedException">
    /// Thrown when the concrete service client implementation does not support service-level query operations.
    /// </exception>
    Task<FeatureServiceQueryObjectIdsResult> QueryObjectIdsAsync(
        FeatureServiceQueryRequest request,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This feature service client does not support service-level query operations.");

    /// <summary>
    /// Executes a service-level <c>query</c> request that returns unique IDs for one or more layers or tables.
    /// </summary>
    /// <param name="request">
    /// The service-level query request.
    /// </param>
    /// <param name="cancellationToken">
    /// The cancellation token.
    /// </param>
    /// <returns>
    /// The layer-grouped unique IDs returned by the feature service.
    /// </returns>
    /// <exception cref="NotSupportedException">
    /// Thrown when the concrete service client implementation does not support service-level query operations.
    /// </exception>
    Task<FeatureServiceQueryUniqueIdsResult> QueryUniqueIdsAsync(
        FeatureServiceQueryRequest request,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This feature service client does not support service-level query operations.");

    /// <summary>
    /// Executes extent-only layer queries for the layer definitions in a service-level query request.
    /// </summary>
    /// <param name="request">
    /// The service-level query request used to derive the layer extent queries.
    /// </param>
    /// <param name="cancellationToken">
    /// The cancellation token.
    /// </param>
    /// <returns>
    /// The layer-grouped extents returned by the feature service.
    /// </returns>
    /// <remarks>
    /// ArcGIS REST exposes <c>returnExtentOnly</c> on layer-level query endpoints. This method executes one
    /// layer-level query per layer definition and groups the results by layer ID.
    /// </remarks>
    /// <exception cref="NotSupportedException">
    /// Thrown when the concrete service client implementation does not support service-level query operations.
    /// </exception>
    Task<FeatureServiceQueryExtentsResult> QueryExtentsAsync(
        FeatureServiceQueryRequest request,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This feature service client does not support service-level query operations.");
}