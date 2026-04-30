using S100Framework.REST.Models;

namespace S100Framework.REST.Clients;

/// <summary>
/// Provides direct layer-level <c>deleteFeatures</c> wrapper methods for <see cref="FeatureLayerClient" />.
/// </summary>
public sealed partial class FeatureLayerClient
{
    /// <inheritdoc />
    public Task<DeleteFeaturesResult> DeleteFeaturesAsync(
        DeleteFeaturesRequest request,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(request);

        request.Validate();

        return _serviceClient.DeleteFeaturesAsync(
            _layerId,
            request,
            cancellationToken);
    }
}