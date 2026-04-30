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
}