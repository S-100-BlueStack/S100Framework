using S100Framework.REST.Exceptions;
using S100Framework.REST.Models;

namespace S100Framework.REST.Clients;

/// <summary>
/// Provides layer-level SQL validation wrapper methods for <see cref="FeatureLayerClient" />.
/// </summary>
public sealed partial class FeatureLayerClient
{
    /// <inheritdoc />
    public async Task<ValidateSqlResult> ValidateSqlAsync(
        ValidateSqlRequest request,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(request);

        request.Validate();

        var schema = await GetSchemaAsync(cancellationToken);

        if (!schema.Capabilities.SupportsValidateSql) {
            throw new FeatureServiceCapabilityException(
                $"Layer '{schema.Name}' ({schema.LayerId}) does not advertise validateSQL support.");
        }

        return await _serviceClient.ValidateSqlAsync(_layerId, request, cancellationToken);
    }
}