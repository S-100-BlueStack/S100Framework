using S100Framework.REST.Models;

namespace S100Framework.REST.Abstractions;

/// <summary>
/// Defines direct layer-level <c>deleteFeatures</c> operations for a feature layer endpoint.
/// </summary>
public partial interface IFeatureLayerClient
{
    /// <summary>
    /// Deletes features from the current layer or table by calling the direct ArcGIS REST <c>deleteFeatures</c> endpoint.
    /// </summary>
    /// <param name="request">
    /// The delete features request.
    /// </param>
    /// <param name="cancellationToken">
    /// The cancellation token.
    /// </param>
    /// <returns>
    /// The delete result returned by the service.
    /// </returns>
    /// <exception cref="NotSupportedException">
    /// Thrown when the concrete layer client implementation does not support direct delete feature operations.
    /// </exception>
    Task<DeleteFeaturesResult> DeleteFeaturesAsync(
        DeleteFeaturesRequest request,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This feature layer client does not support direct deleteFeatures operations.");
}