using System.Runtime.CompilerServices;
using S100Framework.REST.Abstractions;
using S100Framework.REST.Models;

namespace S100Framework.REST.Extensions;

public static class FeatureLayerClientMappingExtensions
{
    public static async IAsyncEnumerable<T> QueryAsync<T>(
        this IFeatureLayerClient layerClient,
        FeatureQuery query,
        Func<FeatureRecord, T> map,
        [EnumeratorCancellation] CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(layerClient);
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(map);

        await foreach (var feature in layerClient.QueryAsync(query, cancellationToken)) {
            yield return map(feature);
        }
    }
}