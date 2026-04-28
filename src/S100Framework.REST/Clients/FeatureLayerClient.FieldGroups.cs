using S100Framework.REST.Models;

namespace S100Framework.REST.Clients;

/// <summary>
/// Provides field group wrapper methods for <see cref="FeatureLayerClient" />.
/// </summary>
public sealed partial class FeatureLayerClient
{
    /// <inheritdoc />
    public Task<FeatureLayerFieldGroupsResult> GetFieldGroupsAsync(
        CancellationToken cancellationToken = default) {
        return _serviceClient.GetFieldGroupsAsync(_layerId, cancellationToken);
    }
}