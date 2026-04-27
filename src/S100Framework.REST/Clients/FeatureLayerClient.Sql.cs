using S100Framework.REST.Models;

namespace S100Framework.REST.Clients;

/// <summary>
/// Provides layer-level SQL validation wrapper methods for <see cref="FeatureLayerClient"/>.
/// </summary>
public sealed partial class FeatureLayerClient
{
    /// <inheritdoc />
    public Task<ValidateSqlResult> ValidateSqlAsync(
        ValidateSqlRequest request,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(request);

        return _serviceClient.ValidateSqlAsync(_layerId, request, cancellationToken);
    }
}