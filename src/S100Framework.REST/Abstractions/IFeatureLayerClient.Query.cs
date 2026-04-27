using S100Framework.REST.Models;

namespace S100Framework.REST.Abstractions;

/// <summary>
/// Defines standard feature query operations for a feature layer endpoint.
/// </summary>
public partial interface IFeatureLayerClient
{
    /// <summary>
    /// Executes a feature query and streams the matching records.
    /// </summary>
    /// <param name="query">
    /// The feature query to execute.
    /// </param>
    /// <param name="cancellationToken">
    /// The cancellation token.
    /// </param>
    /// <returns>
    /// An async stream of matching feature records.
    /// </returns>
    IAsyncEnumerable<FeatureRecord> QueryAsync(
        FeatureQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a feature query and returns only the total record count.
    /// </summary>
    /// <param name="query">
    /// The feature query to execute.
    /// </param>
    /// <param name="cancellationToken">
    /// The cancellation token.
    /// </param>
    /// <returns>
    /// The total number of matching records.
    /// </returns>
    Task<long> QueryCountAsync(
        FeatureQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a feature query and returns only the matching object IDs.
    /// </summary>
    /// <param name="query">
    /// The feature query to execute.
    /// </param>
    /// <param name="cancellationToken">
    /// The cancellation token.
    /// </param>
    /// <returns>
    /// The matching object IDs.
    /// </returns>
    Task<IReadOnlyList<long>> QueryObjectIdsAsync(
        FeatureQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a feature query and returns only the matching unique IDs.
    /// </summary>
    /// <param name="query">
    /// The feature query to execute.
    /// </param>
    /// <param name="cancellationToken">
    /// The cancellation token.
    /// </param>
    /// <returns>
    /// The matching unique ID result.
    /// </returns>
    Task<FeatureUniqueIdQueryResult> QueryUniqueIdsAsync(
        FeatureQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a feature query and returns the aggregate extent of the matching records.
    /// </summary>
    /// <param name="query">
    /// The feature query to execute.
    /// </param>
    /// <param name="cancellationToken">
    /// The cancellation token.
    /// </param>
    /// <returns>
    /// The matching extent, or <see langword="null" /> when no extent is returned.
    /// </returns>
    Task<FeatureExtent?> QueryExtentAsync(
        FeatureQuery query,
        CancellationToken cancellationToken = default);
}