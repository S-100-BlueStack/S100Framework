using S100Framework.REST.Models;

namespace S100Framework.REST.Abstractions;

/// <summary>
/// Defines service-level relationship resource operations for a feature service endpoint.
/// </summary>
public partial interface IFeatureServiceClient
{
    /// <summary>
    /// Gets relationship class information exposed by the feature service relationships resource.
    /// </summary>
    /// <param name="cancellationToken">
    /// The cancellation token.
    /// </param>
    /// <returns>
    /// The service-level relationship class information returned by the feature service.
    /// </returns>
    /// <exception cref="NotSupportedException">
    /// Thrown when the concrete service client implementation does not support the relationships resource.
    /// </exception>
    Task<FeatureServiceRelationshipsResult> GetRelationshipsAsync(
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This feature service client does not support the relationships resource.");
}