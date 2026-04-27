using S100Framework.REST.Models;

namespace S100Framework.REST.Abstractions;

/// <summary>
/// Defines top-features query operations for a feature layer endpoint.
/// </summary>
public partial interface IFeatureLayerClient
{
    /// <summary>
    /// Executes a top-features query for the current layer.
    /// </summary>
    /// <param name="query">
    /// The top-features query to execute.
    /// </param>
    /// <param name="cancellationToken">
    /// The cancellation token.
    /// </param>
    /// <returns>
    /// The matching top features.
    /// </returns>
    Task<IReadOnlyList<FeatureRecord>> QueryTopFeaturesAsync(
        TopFeaturesQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a top-features query and returns only the matching object IDs.
    /// </summary>
    /// <param name="query">
    /// The top-features query to execute.
    /// </param>
    /// <param name="cancellationToken">
    /// The cancellation token.
    /// </param>
    /// <returns>
    /// The matching top-feature object IDs.
    /// </returns>
    Task<IReadOnlyList<long>> QueryTopFeatureObjectIdsAsync(
        TopFeaturesQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a top-features query and returns only the count result.
    /// </summary>
    /// <param name="query">
    /// The top-features query to execute.
    /// </param>
    /// <param name="cancellationToken">
    /// The cancellation token.
    /// </param>
    /// <returns>
    /// The top-features count result.
    /// </returns>
    Task<TopFeaturesCountResult> QueryTopFeatureCountAsync(
        TopFeaturesQuery query,
        CancellationToken cancellationToken = default);
}