using S100Framework.REST.Models;

namespace S100Framework.REST.Abstractions;

/// <summary>
/// Defines optional data-element query operations for a feature layer endpoint.
/// </summary>
public partial interface IFeatureLayerClient
{
    /// <summary>
    /// Queries the service for the data element associated with the current layer or table.
    /// </summary>
    /// <param name="cancellationToken">
    /// The cancellation token.
    /// </param>
    /// <returns>
    /// The data element returned for the current layer or table.
    /// </returns>
    /// <exception cref="NotSupportedException">
    /// Thrown when the concrete layer client implementation does not support data-element query operations.
    /// </exception>
    Task<FeatureLayerDataElement> QueryDataElementAsync(
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This feature layer client does not support queryDataElements operations.");
}