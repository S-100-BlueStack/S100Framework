using S100Framework.REST.Models;

namespace S100Framework.REST.Abstractions;

/// <summary>
/// Defines related-record query operations for a feature layer endpoint.
/// </summary>
public partial interface IFeatureLayerClient
{
    /// <summary>
    /// Executes a related-records query for the current layer.
    /// </summary>
    /// <param name="query">
    /// The related-records query to execute.
    /// </param>
    /// <param name="cancellationToken">
    /// The cancellation token.
    /// </param>
    /// <returns>
    /// The related record groups returned by the service.
    /// </returns>
    Task<IReadOnlyList<RelatedRecordGroup>> QueryRelatedRecordsAsync(
        RelatedRecordsQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a related-records query that returns only the related record count for each source object ID.
    /// </summary>
    /// <param name="query">
    /// The related-records query to execute.
    /// </param>
    /// <param name="cancellationToken">
    /// The cancellation token.
    /// </param>
    /// <returns>
    /// The related record counts grouped by source object ID.
    /// </returns>
    Task<IReadOnlyList<RelatedRecordCountGroup>> QueryRelatedRecordCountsAsync(
        RelatedRecordsQuery query,
        CancellationToken cancellationToken = default);
}