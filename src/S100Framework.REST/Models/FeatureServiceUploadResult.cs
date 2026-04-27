namespace S100Framework.REST.Models;

/// <summary>
/// Represents the server-side upload item returned from the feature service uploads endpoint.
/// </summary>
/// <param name="ItemId">The upload item ID used by operations such as append.</param>
/// <param name="ItemName">The uploaded item name returned by the server, when available.</param>
/// <param name="Description">The upload description returned by the server, when available.</param>
/// <param name="Date">The server timestamp for the uploaded item, when available.</param>
/// <param name="Committed">Indicates whether the server reports the upload item as committed.</param>
public sealed record FeatureServiceUploadResult(
    string ItemId,
    string? ItemName,
    string? Description,
    long? Date,
    bool? Committed);