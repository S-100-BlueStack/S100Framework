namespace S100Framework.REST.Models;

/// <summary>
/// Represents a replica edit result with layer and operation context.
/// </summary>
/// <param name="LayerId">
/// The layer or table ID that returned the edit result.
/// </param>
/// <param name="Operation">
/// The edit operation that produced the result.
/// </param>
/// <param name="Result">
/// The edit result returned by the service.
/// </param>
public sealed record ReplicaLayerEditResult(
    int LayerId,
    ReplicaEditOperation Operation,
    ReplicaEditResult Result)
{
    /// <summary>
    /// Gets a value indicating whether the edit result explicitly succeeded.
    /// </summary>
    public bool IsSuccessful => Result.IsSuccessful;

    /// <summary>
    /// Gets a value indicating whether the edit result failed or contains an error object.
    /// </summary>
    public bool IsFailed => Result.IsFailed;

    /// <summary>
    /// Gets the object ID returned by the service, when available.
    /// </summary>
    public long? ObjectId => Result.ObjectId;

    /// <summary>
    /// Gets the global ID returned by the service, when available.
    /// </summary>
    public string? GlobalId => Result.GlobalId;

    /// <summary>
    /// Gets the edit error code returned by the service, when available.
    /// </summary>
    public int? ErrorCode => Result.ErrorCode;

    /// <summary>
    /// Gets the edit error description returned by the service, when available.
    /// </summary>
    public string? ErrorDescription => Result.ErrorDescription;

    /// <summary>
    /// Gets the edit error details returned by the service.
    /// </summary>
    public IReadOnlyList<string> ErrorDetails => Result.ErrorDetails;
}