using S100Framework.REST.Models;

namespace S100Framework.REST.Abstractions;

/// <summary>
/// Defines the public contract for working with a feature service root endpoint.
/// </summary>
/// <remarks>
/// Operation-specific members are declared in partial interface files to keep each Feature Service operation group
/// easier to review and maintain without changing the public interface type.
/// </remarks>
public partial interface IFeatureServiceClient
{
    /// <summary>
    /// Gets metadata for the current feature service.
    /// </summary>
    /// <param name="cancellationToken">
    /// The cancellation token.
    /// </param>
    /// <returns>
    /// The service metadata.
    /// </returns>
    Task<FeatureServiceMetadata> GetMetadataAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a layer client for the specified layer ID.
    /// </summary>
    /// <param name="layerId">
    /// The layer ID.
    /// </param>
    /// <returns>
    /// A layer client bound to the specified layer.
    /// </returns>
    IFeatureLayerClient GetLayerClient(int layerId);

    /// <summary>
    /// Resolves a layer or table by name and creates a client for the matching dataset.
    /// </summary>
    /// <param name="layerName">
    /// The layer or table name to look up.
    /// </param>
    /// <param name="cancellationToken">
    /// The cancellation token.
    /// </param>
    /// <returns>
    /// A layer client bound to the resolved layer or table.
    /// </returns>
    Task<IFeatureLayerClient> GetLayerClientAsync(
        string layerName,
        CancellationToken cancellationToken = default);
}