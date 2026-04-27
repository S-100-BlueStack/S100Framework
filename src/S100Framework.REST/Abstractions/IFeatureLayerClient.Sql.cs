using S100Framework.REST.Models;

namespace S100Framework.REST.Abstractions;

/// <summary>
/// Defines optional SQL validation operations for a feature layer endpoint.
/// </summary>
public partial interface IFeatureLayerClient
{
    /// <summary>
    /// Validates an SQL expression or WHERE clause against the current layer.
    /// </summary>
    /// <param name="request">The SQL validation request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The SQL validation result returned by the service.</returns>
    /// <exception cref="NotSupportedException">
    /// Thrown when the concrete layer client implementation does not support SQL validation.
    /// </exception>
    Task<ValidateSqlResult> ValidateSqlAsync(
        ValidateSqlRequest request,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This feature layer client does not support SQL validation.");
}