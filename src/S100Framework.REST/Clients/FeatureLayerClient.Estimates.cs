using S100Framework.REST.Models;

namespace S100Framework.REST.Clients;

/// <summary>
/// Provides layer-level estimate wrapper methods for <see cref="FeatureLayerClient"/>.
/// </summary>
public sealed partial class FeatureLayerClient
{
    /// <inheritdoc />
    public Task<FeatureLayerEstimate> GetEstimatesAsync(
        CancellationToken cancellationToken = default) {
        return _serviceClient.GetLayerEstimatesAsync(_layerId, cancellationToken);
    }
}