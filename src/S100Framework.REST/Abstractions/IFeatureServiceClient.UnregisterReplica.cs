using S100Framework.REST.Models;

namespace S100Framework.REST.Abstractions;

/// <summary>
/// Defines service-level replica unregister operations for a feature service endpoint.
/// </summary>
public partial interface IFeatureServiceClient
{
    /// <summary>
    /// Unregisters a replica from the feature service.
    /// </summary>
    /// <param name="request">
    /// The unregister replica request.
    /// </param>
    /// <param name="cancellationToken">
    /// The cancellation token.
    /// </param>
    /// <returns>
    /// The unregister result returned by the service.
    /// </returns>
    Task<UnregisterReplicaResult> UnregisterReplicaAsync(
        UnregisterReplicaRequest request,
        CancellationToken cancellationToken = default);
}