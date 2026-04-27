using S100Framework.REST.Models;

namespace S100Framework.REST.Abstractions;

/// <summary>
/// Defines statistics query operations for a feature layer endpoint.
/// </summary>
public partial interface IFeatureLayerClient
{
    /// <summary>
    /// Executes a statistics query for the current layer or table.
    /// </summary>
    /// <param name="query">
    /// The statistics query to execute.
    /// </param>
    /// <param name="cancellationToken">
    /// The cancellation token.
    /// </param>
    /// <returns>
    /// The resulting statistic rows.
    /// </returns>
    Task<IReadOnlyList<StatisticRow>> QueryStatisticsAsync(
        FeatureStatisticsQuery query,
        CancellationToken cancellationToken = default);
}