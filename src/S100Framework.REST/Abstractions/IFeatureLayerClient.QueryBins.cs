using S100Framework.REST.Models;

namespace S100Framework.REST.Abstractions;

/// <summary>
/// Defines optional bin query operations for a feature layer endpoint.
/// </summary>
public partial interface IFeatureLayerClient
{
    /// <summary>
    /// Executes a <c>queryBins</c> request for the current layer or table.
    /// </summary>
    /// <param name="request">The bin query request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The bin query result returned by the service.</returns>
    /// <exception cref="NotSupportedException">
    /// Thrown when the concrete layer client implementation does not support bin query operations.
    /// </exception>
    Task<QueryBinsResult> QueryBinsAsync(
        QueryBinsRequest request,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This feature layer client does not support queryBins operations.");
}