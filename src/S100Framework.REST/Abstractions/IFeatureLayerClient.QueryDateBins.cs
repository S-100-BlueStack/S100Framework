using S100Framework.REST.Models;

namespace S100Framework.REST.Abstractions;

/// <summary>
/// Defines optional date-bin query operations for a feature layer endpoint.
/// </summary>
public partial interface IFeatureLayerClient
{
    /// <summary>
    /// Executes a <c>queryDateBins</c> request for the current layer or table.
    /// </summary>
    /// <param name="request">The date-bin query request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The date-bin query result returned by the service.</returns>
    /// <exception cref="NotSupportedException">
    /// Thrown when the concrete layer client implementation does not support date-bin query operations.
    /// </exception>
    Task<QueryDateBinsResult> QueryDateBinsAsync(
        QueryDateBinsRequest request,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This feature layer client does not support queryDateBins operations.");
}