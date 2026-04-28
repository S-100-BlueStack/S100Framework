using S100Framework.REST.Models;

namespace S100Framework.REST.Abstractions;

/// <summary>
/// Defines optional analytic query operations for a feature layer endpoint.
/// </summary>
public partial interface IFeatureLayerClient
{
    /// <summary>
    /// Executes a <c>queryAnalytic</c> request for the current layer or table.
    /// </summary>
    /// <param name="request">
    /// The analytic query request.
    /// </param>
    /// <param name="cancellationToken">
    /// The cancellation token.
    /// </param>
    /// <returns>
    /// The analytic query result returned by the service.
    /// </returns>
    /// <exception cref="NotSupportedException">
    /// Thrown when the concrete layer client implementation does not support analytic query operations.
    /// </exception>
    Task<QueryAnalyticResult> QueryAnalyticAsync(
        QueryAnalyticRequest request,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This feature layer client does not support queryAnalytic operations.");
}