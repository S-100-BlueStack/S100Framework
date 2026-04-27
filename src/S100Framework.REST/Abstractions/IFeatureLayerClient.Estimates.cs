using S100Framework.REST.Models;

namespace S100Framework.REST.Abstractions;

/// <summary>
/// Defines optional layer-level estimate operations for a feature layer endpoint.
/// </summary>
public partial interface IFeatureLayerClient
{
    /// <summary>
    /// Gets approximate count and extent information for the current layer or table.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The layer estimate returned by the service.</returns>
    /// <exception cref="NotSupportedException">
    /// Thrown when the concrete layer client implementation does not support estimate operations.
    /// </exception>
    Task<FeatureLayerEstimate> GetEstimatesAsync(
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This feature layer client does not support estimate operations.");
}