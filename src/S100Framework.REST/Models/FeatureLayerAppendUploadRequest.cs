namespace S100Framework.REST.Models;

/// <summary>
/// Represents a layer-level append request that uses a server upload item as the source.
/// </summary>
public sealed record FeatureLayerAppendUploadRequest : FeatureLayerAppendRequestBase
{
    /// <summary>
    /// Gets the upload item ID returned from the uploads operation.
    /// </summary>
    public string AppendUploadId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the uploaded source format.
    /// </summary>
    public FeatureServiceAppendSourceFormat? AppendUploadFormat { get; init; }

    /// <summary>
    /// Validates the append request.
    /// </summary>
    public void Validate() {
        ValidateCommon();

        if (string.IsNullOrWhiteSpace(AppendUploadId)) {
            throw new InvalidOperationException("AppendUploadId must be provided.");
        }

        if (!AppendUploadFormat.HasValue) {
            throw new InvalidOperationException("AppendUploadFormat must be provided.");
        }

        if (!Enum.IsDefined(AppendUploadFormat.Value)) {
            throw new InvalidOperationException("AppendUploadFormat must be a supported append source format.");
        }
    }
}