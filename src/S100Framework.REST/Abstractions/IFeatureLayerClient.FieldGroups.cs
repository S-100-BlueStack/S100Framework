using S100Framework.REST.Models;

namespace S100Framework.REST.Abstractions;

/// <summary>
/// Defines optional field group operations for a feature layer endpoint.
/// </summary>
public partial interface IFeatureLayerClient
{
    /// <summary>
    /// Gets field groups defined for the current layer or table.
    /// </summary>
    /// <param name="cancellationToken">
    /// The cancellation token.
    /// </param>
    /// <returns>
    /// The field groups returned by the service.
    /// </returns>
    /// <exception cref="NotSupportedException">
    /// Thrown when the concrete layer client implementation does not support field group operations.
    /// </exception>
    Task<FeatureLayerFieldGroupsResult> GetFieldGroupsAsync(
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This feature layer client does not support field group operations.");
}