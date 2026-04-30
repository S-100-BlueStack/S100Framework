using S100Framework.REST.Models;

namespace S100Framework.REST.Abstractions;

/// <summary>
/// Defines direct layer-level <c>addFeatures</c> and <c>updateFeatures</c> operations for a feature layer endpoint.
/// </summary>
public partial interface IFeatureLayerClient
{
    /// <summary>
    /// Adds features to the current layer or table by calling the direct ArcGIS REST <c>addFeatures</c> endpoint.
    /// </summary>
    /// <param name="request">
    /// The add features request.
    /// </param>
    /// <param name="cancellationToken">
    /// The cancellation token.
    /// </param>
    /// <returns>
    /// The add result returned by the service.
    /// </returns>
    /// <exception cref="NotSupportedException">
    /// Thrown when the concrete layer client implementation does not support direct add feature operations.
    /// </exception>
    Task<AddFeaturesResult> AddFeaturesAsync(
        AddFeaturesRequest request,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This feature layer client does not support direct addFeatures operations.");

    /// <summary>
    /// Updates features in the current layer or table by calling the direct ArcGIS REST <c>updateFeatures</c> endpoint.
    /// </summary>
    /// <param name="request">
    /// The update features request.
    /// </param>
    /// <param name="cancellationToken">
    /// The cancellation token.
    /// </param>
    /// <returns>
    /// The update result returned by the service.
    /// </returns>
    /// <exception cref="NotSupportedException">
    /// Thrown when the concrete layer client implementation does not support direct update feature operations.
    /// </exception>
    Task<UpdateFeaturesResult> UpdateFeaturesAsync(
        UpdateFeaturesRequest request,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This feature layer client does not support direct updateFeatures operations.");
}