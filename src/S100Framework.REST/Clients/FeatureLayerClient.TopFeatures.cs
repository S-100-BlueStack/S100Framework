using S100Framework.REST.Internal.Dto;
using S100Framework.REST.Models;

namespace S100Framework.REST.Clients;

/// <summary>
/// Provides top-features query operations for <see cref="FeatureLayerClient" />.
/// </summary>
public sealed partial class FeatureLayerClient
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<FeatureRecord>> QueryTopFeaturesAsync(
        TopFeaturesQuery query,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(query);

        var schema = await GetSchemaAsync(cancellationToken);
        var response = await _serviceClient.QueryTopFeaturesAsync(_layerId, query, cancellationToken);

        return (response.Features ?? new List<EsriFeatureDto>())
            .Select(feature => MapFeature(schema, feature))
            .ToArray();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<long>> QueryTopFeatureObjectIdsAsync(
        TopFeaturesQuery query,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(query);

        var response = await _serviceClient.QueryTopFeatureIdsAsync(_layerId, query, cancellationToken);
        return response.ObjectIds?.ToArray() ?? Array.Empty<long>();
    }

    /// <inheritdoc />
    public Task<TopFeaturesCountResult> QueryTopFeatureCountAsync(
        TopFeaturesQuery query,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(query);

        return _serviceClient.QueryTopFeatureCountAsync(_layerId, query, cancellationToken);
    }
}