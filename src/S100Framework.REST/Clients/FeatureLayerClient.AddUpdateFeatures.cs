using S100Framework.REST.Models;

namespace S100Framework.REST.Clients;

/// <summary>
/// Provides direct layer-level <c>addFeatures</c> and <c>updateFeatures</c> wrapper methods for <see cref="FeatureLayerClient" />.
/// </summary>
public sealed partial class FeatureLayerClient
{
    /// <inheritdoc />
    public Task<AddFeaturesResult> AddFeaturesAsync(
        AddFeaturesRequest request,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(request);

        request.Validate();

        return _serviceClient.AddFeaturesAsync(
            _layerId,
            request,
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<UpdateFeaturesResult> UpdateFeaturesAsync(
        UpdateFeaturesRequest request,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(request);

        request.Validate();

        return _serviceClient.UpdateFeaturesAsync(
            _layerId,
            request,
            cancellationToken);
    }
}