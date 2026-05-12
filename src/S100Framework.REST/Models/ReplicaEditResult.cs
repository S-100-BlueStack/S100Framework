namespace S100Framework.REST.Models;

/// <summary>
/// Represents a single upload edit result returned in a replica JSON result file.
/// </summary>
/// <param name="ObjectId">
/// The object ID returned by the service, when available.
/// </param>
/// <param name="GlobalId">
/// The global ID returned by the service, when available.
/// </param>
/// <param name="Success">
/// Indicates whether the edit succeeded, when the service returns a success value.
/// </param>
/// <param name="ErrorCode">
/// The edit error code returned by the service, when available.
/// </param>
/// <param name="ErrorDescription">
/// The edit error description returned by the service, when available.
/// </param>
/// <param name="ErrorDetails">
/// The edit error details returned by the service.
/// </param>
/// <param name="RawJson">
/// The raw JSON for the edit result.
/// </param>
public sealed record ReplicaEditResult(
    long? ObjectId,
    string? GlobalId,
    bool? Success,
    int? ErrorCode,
    string? ErrorDescription,
    IReadOnlyList<string> ErrorDetails,
    string RawJson)
{
    /// <summary>
    /// Gets a value indicating whether the edit result contains an error object.
    /// </summary>
    public bool HasError =>
        ErrorCode.HasValue ||
        !string.IsNullOrWhiteSpace(ErrorDescription) ||
        ErrorDetails.Count > 0;
}