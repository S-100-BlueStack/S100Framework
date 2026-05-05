namespace S100Framework.REST.Models;

/// <summary>
/// Represents a layer-level append request that uses a portal item as the source.
/// </summary>
public sealed record FeatureLayerAppendItemRequest : FeatureLayerAppendRequestBase
{
    /// <summary>
    /// Gets the portal item ID used as the append source.
    /// </summary>
    public string AppendItemId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the source format when it must be specified explicitly.
    /// </summary>
    public FeatureServiceAppendSourceFormat? AppendUploadFormat { get; init; }

    /// <summary>
    /// Validates the append request.
    /// </summary>
    public void Validate() {
        ValidateCommon();

        if (string.IsNullOrWhiteSpace(AppendItemId)) {
            throw new InvalidOperationException("AppendItemId must be provided.");
        }

        if (AppendUploadFormat.HasValue && !Enum.IsDefined(AppendUploadFormat.Value)) {
            throw new InvalidOperationException("AppendUploadFormat must be a supported append source format.");
        }
    }
}