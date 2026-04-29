using S100Framework.REST.Models;

namespace S100Framework.REST.Abstractions;

/// <summary>
/// Defines optional upload operations for feature service server-side upload items.
/// </summary>
public partial interface IFeatureServiceClient
{
    /// <summary>
    /// Uploads a file to the feature service uploads endpoint.
    /// </summary>
    /// <param name="request">The upload request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The uploaded server-side item metadata.</returns>
    /// <exception cref="NotSupportedException">
    /// Thrown when the concrete service client implementation does not support upload operations.
    /// </exception>
    Task<FeatureServiceUploadResult> UploadItemAsync(
        FeatureServiceUploadRequest request,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This feature service client does not support upload operations.");

    /// <summary>
    /// Deletes a server-side upload item from the feature service uploads endpoint.
    /// </summary>
    /// <param name="itemId">
    /// The upload item ID to delete.
    /// </param>
    /// <param name="cancellationToken">
    /// The cancellation token.
    /// </param>
    /// <returns>
    /// The delete result returned by the service.
    /// </returns>
    /// <exception cref="NotSupportedException">
    /// Thrown when the concrete service client implementation does not support upload delete operations.
    /// </exception>
    Task<FeatureServiceUploadDeleteResult> DeleteUploadItemAsync(
        string itemId,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This feature service client does not support upload delete operations.");
}