using S100Framework.REST.Models;

namespace S100Framework.REST.Abstractions;

/// <summary>
/// Defines optional calculate operations for a feature layer endpoint.
/// </summary>
public partial interface IFeatureLayerClient
{
    /// <summary>
    /// Executes a layer-level <c>calculate</c> request for the current layer or table.
    /// </summary>
    /// <param name="request">
    /// The calculate request.
    /// </param>
    /// <param name="cancellationToken">
    /// The cancellation token.
    /// </param>
    /// <returns>
    /// The calculate result returned by the service.
    /// </returns>
    /// <exception cref="NotSupportedException">
    /// Thrown when the concrete layer client implementation does not support calculate operations.
    /// </exception>
    Task<CalculateResult> CalculateAsync(
        CalculateRequest request,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This feature layer client does not support calculate operations.");
}