using S100Framework.REST.Models;

namespace S100Framework.REST.Abstractions;

/// <summary>
/// Defines contingent value operations for a feature service endpoint.
/// </summary>
public partial interface IFeatureServiceClient
{
    /// <summary>
    /// Queries contingent values for one or more layers or tables in the feature service.
    /// </summary>
    /// <param name="request">
    /// The contingent values query request.
    /// </param>
    /// <param name="cancellationToken">
    /// The cancellation token.
    /// </param>
    /// <returns>
    /// The raw contingent values payload returned by the service.
    /// </returns>
    /// <exception cref="NotSupportedException">
    /// Thrown when the concrete service client implementation does not support contingent value operations.
    /// </exception>
    Task<FeatureServiceContingentValuesResult> QueryContingentValuesAsync(
        QueryContingentValuesRequest request,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This feature service client does not support contingent value operations.");
}