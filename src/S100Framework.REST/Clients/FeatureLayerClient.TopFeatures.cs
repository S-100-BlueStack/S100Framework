using S100Framework.REST.Exceptions;
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

        query.Validate();

        var schema = await GetSchemaAsync(cancellationToken);
        EnsureTopFeaturesQuerySupported(schema);

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

        query.Validate();

        if (query.ObjectIds is { Count: > 0 }) {
            throw new InvalidOperationException(
                "ObjectIds cannot be combined with returnIdsOnly for queryTopFeatures.");
        }

        var schema = await GetSchemaAsync(cancellationToken);
        EnsureTopFeaturesQuerySupported(schema);

        var response = await _serviceClient.QueryTopFeatureIdsAsync(_layerId, query, cancellationToken);
        return response.ObjectIds?.ToArray() ?? Array.Empty<long>();
    }

    /// <inheritdoc />
    public async Task<TopFeaturesCountResult> QueryTopFeatureCountAsync(
        TopFeaturesQuery query,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(query);

        query.Validate();

        var schema = await GetSchemaAsync(cancellationToken);
        EnsureTopFeaturesQuerySupported(schema);

        return await _serviceClient.QueryTopFeatureCountAsync(_layerId, query, cancellationToken);
    }

    private static void EnsureTopFeaturesQuerySupported(FeatureLayerSchema schema) {
        if (!schema.Capabilities.SupportsTopFeaturesQuery) {
            throw new FeatureServiceCapabilityException(
                $"Layer '{schema.Name}' ({schema.LayerId}) does not support top-features queries.");
        }
    }
}