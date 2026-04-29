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
}