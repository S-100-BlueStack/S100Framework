using S100Framework.REST.Models;

namespace S100Framework.REST.Clients;

/// <summary>
/// Provides layer-level data-element query wrapper methods for <see cref="FeatureLayerClient" />.
/// </summary>
public sealed partial class FeatureLayerClient
{
    /// <inheritdoc />
    public async Task<FeatureLayerDataElement> QueryDataElementAsync(
        CancellationToken cancellationToken = default) {
        var dataElements = await _serviceClient.QueryDataElementsAsync(
            [_layerId],
            cancellationToken);

        return dataElements.SingleOrDefault()
               ?? throw new InvalidOperationException(
                   $"The feature service did not return a data element for layer {_layerId}.");
    }
}