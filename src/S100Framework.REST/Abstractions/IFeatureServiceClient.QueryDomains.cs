using S100Framework.REST.Models;

namespace S100Framework.REST.Abstractions;

/// <summary>
/// Defines domain-query operations for a feature service.
/// </summary>
public partial interface IFeatureServiceClient
{
    /// <summary>
    /// Queries the service for domains referenced by the specified layers.
    /// </summary>
    /// <param name="layerIds">
    /// The layer IDs to inspect.
    /// </param>
    /// <param name="cancellationToken">
    /// The cancellation token.
    /// </param>
    /// <returns>
    /// The referenced domains.
    /// </returns>
    Task<IReadOnlyList<FeatureServiceDomain>> QueryDomainsAsync(
        IReadOnlyList<int> layerIds,
        CancellationToken cancellationToken = default) {
        throw new NotSupportedException(
            "Domain queries are not supported by this feature service client implementation.");
    }
}