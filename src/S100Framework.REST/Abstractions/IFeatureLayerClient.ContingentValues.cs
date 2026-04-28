using S100Framework.REST.Models;

namespace S100Framework.REST.Abstractions;

/// <summary>
/// Defines optional contingent value operations for a feature layer endpoint.
/// </summary>
public partial interface IFeatureLayerClient
{
    /// <summary>
    /// Queries contingent values for the current layer or table.
    /// </summary>
    /// <param name="options">
    /// Optional contingent value query options.
    /// </param>
    /// <param name="cancellationToken">
    /// The cancellation token.
    /// </param>
    /// <returns>
    /// The contingent values returned for the current layer or table.
    /// </returns>
    /// <exception cref="NotSupportedException">
    /// Thrown when the concrete layer client implementation does not support contingent value operations.
    /// </exception>
    Task<FeatureLayerContingentValuesResult> QueryContingentValuesAsync(
        QueryContingentValuesOptions? options = null,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This feature layer client does not support contingent value operations.");
}