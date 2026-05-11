using S100Framework.REST.Models;

namespace S100Framework.REST.Abstractions;

/// <summary>
/// Defines service-level replica listing operations for a feature service endpoint.
/// </summary>
public partial interface IFeatureServiceClient
{
    /// <summary>
    /// Gets the replicas registered for the feature service.
    /// </summary>
    /// <param name="request">
    /// Optional replica listing request parameters.
    /// </param>
    /// <param name="cancellationToken">
    /// The cancellation token.
    /// </param>
    /// <returns>
    /// The replicas returned by the service.
    /// </returns>
    Task<FeatureServiceReplicasResult> GetReplicasAsync(
        FeatureServiceReplicasRequest? request = null,
        CancellationToken cancellationToken = default);
}