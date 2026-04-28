using S100Framework.REST.Models;

namespace S100Framework.REST.Abstractions;

/// <summary>
/// Defines data-element query operations for a feature service endpoint.
/// </summary>
public partial interface IFeatureServiceClient
{
    /// <summary>
    /// Queries the service for data elements associated with the specified layers or tables.
    /// </summary>
    /// <param name="layerIds">
    /// The layer or table IDs to inspect.
    /// </param>
    /// <param name="cancellationToken">
    /// The cancellation token.
    /// </param>
    /// <returns>
    /// The data elements returned by the service.
    /// </returns>
    /// <exception cref="NotSupportedException">
    /// Thrown when the concrete service client implementation does not support data-element query operations.
    /// </exception>
    Task<IReadOnlyList<FeatureLayerDataElement>> QueryDataElementsAsync(
        IReadOnlyList<int> layerIds,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This feature service client does not support queryDataElements operations.");
}