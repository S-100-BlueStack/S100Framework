using S100Framework.REST.Models;

namespace S100Framework.REST.Abstractions;

/// <summary>
/// Defines optional service-level estimate operations for a feature service endpoint.
/// </summary>
public partial interface IFeatureServiceClient
{
    /// <summary>
    /// Gets approximate count and extent information for all layers and tables that the service returns.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The layer estimates returned by the service.</returns>
    /// <exception cref="NotSupportedException">
    /// Thrown when the concrete service client implementation does not support estimate operations.
    /// </exception>
    Task<IReadOnlyList<FeatureLayerEstimate>> GetEstimatesAsync(
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This feature service client does not support estimate operations.");

    /// <summary>
    /// Gets approximate count and extent information for the specified layers or tables.
    /// </summary>
    /// <param name="layerIds">The layer or table IDs to include in the estimate request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The layer estimates returned by the service.</returns>
    /// <exception cref="NotSupportedException">
    /// Thrown when the concrete service client implementation does not support estimate operations.
    /// </exception>
    Task<IReadOnlyList<FeatureLayerEstimate>> GetEstimatesAsync(
        IReadOnlyList<int> layerIds,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This feature service client does not support estimate operations.");
}