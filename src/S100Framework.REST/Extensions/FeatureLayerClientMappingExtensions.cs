using System.Runtime.CompilerServices;
using S100Framework.REST.Abstractions;
using S100Framework.REST.Models;

namespace S100Framework.REST.Extensions;

/// <summary>
/// Provides lightweight mapping helpers for feature layer queries.
/// </summary>
public static class FeatureLayerClientMappingExtensions
{
    /// <summary>
    /// Executes a query and projects each returned <see cref="FeatureRecord"/> into a custom result type.
    /// </summary>
    /// <typeparam name="T">
    /// The mapped result type.
    /// </typeparam>
    /// <param name="layerClient">
    /// The feature layer client used to execute the query.
    /// </param>
    /// <param name="query">
    /// The feature query to execute.
    /// </param>
    /// <param name="map">
    /// A projection function that converts each <see cref="FeatureRecord"/> into <typeparamref name="T"/>.
    /// </param>
    /// <param name="cancellationToken">
    /// The cancellation token.
    /// </param>
    /// <returns>
    /// An async stream of mapped query results.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="layerClient"/>, <paramref name="query"/>, or <paramref name="map"/> is <see langword="null"/>.
    /// </exception>
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