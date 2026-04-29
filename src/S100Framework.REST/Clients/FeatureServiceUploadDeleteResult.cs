namespace S100Framework.REST.Models;

/// <summary>
/// Represents the result of deleting a server-side feature service upload item.
/// </summary>
/// <param name="ItemId">
/// The upload item ID that was requested for deletion.
/// </param>
/// <param name="Success">
/// Indicates whether the service reported a successful delete operation.
/// </param>
public sealed record FeatureServiceUploadDeleteResult(
    string ItemId,
    bool Success);