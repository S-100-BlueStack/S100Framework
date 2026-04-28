using S100Framework.REST.Models;

namespace S100Framework.REST.Clients;

/// <summary>
/// Provides layer-level contingent value wrapper methods for <see cref="FeatureLayerClient" />.
/// </summary>
public sealed partial class FeatureLayerClient
{
    /// <inheritdoc />
    public async Task<FeatureLayerContingentValuesResult> QueryContingentValuesAsync(
        QueryContingentValuesOptions? options = null,
        CancellationToken cancellationToken = default) {
        options ??= new QueryContingentValuesOptions();

        var result = await _serviceClient.QueryContingentValuesAsync(
            new QueryContingentValuesRequest {
                LayerIds = [_layerId],
                CompactFormat = options.CompactFormat,
                DomainDictionaries = options.DomainDictionaries
            },
            cancellationToken);

        return new FeatureLayerContingentValuesResult(
            _layerId,
            result.Payload);
    }
}