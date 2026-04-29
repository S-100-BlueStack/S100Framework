using S100Framework.REST.Exceptions;
using S100Framework.REST.Models;

namespace S100Framework.REST.Clients;

/// <summary>
/// Provides layer-level calculate wrapper methods for <see cref="FeatureLayerClient" />.
/// </summary>
public sealed partial class FeatureLayerClient
{
    /// <inheritdoc />
    public async Task<CalculateResult> CalculateAsync(
        CalculateRequest request,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(request);

        request.Validate();

        var schema = await GetSchemaAsync(cancellationToken);

        if (!schema.Capabilities.SupportsCalculate) {
            throw new FeatureServiceCapabilityException(
                $"Layer '{schema.Name}' ({schema.LayerId}) does not advertise calculate support.");
        }

        return await _serviceClient.CalculateAsync(
            _layerId,
            request,
            cancellationToken);
    }
}