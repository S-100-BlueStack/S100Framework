namespace S100Framework.REST.Models;

/// <summary>
/// Represents the final result of a layer-level <c>applyEdits</c> operation.
/// </summary>
/// <param name="AddResults">The per-feature results for add operations.</param>
/// <param name="UpdateResults">The per-feature results for update operations.</param>
/// <param name="DeleteResults">The per-feature results for delete operations.</param>
public sealed record ApplyEditsResult(
    IReadOnlyList<EditResult> AddResults,
    IReadOnlyList<EditResult> UpdateResults,
    IReadOnlyList<EditResult> DeleteResults);

/// <summary>
/// Represents the result of a single add, update, or delete operation within <c>applyEdits</c>.
/// </summary>
/// <param name="Success">
/// Indicates whether the individual edit operation succeeded.
/// </param>
/// <param name="ObjectId">
/// The object ID returned by the server, when available.
/// </param>
/// <param name="GlobalId">
/// The global ID returned by the server, when available.
/// </param>
/// <param name="ErrorCode">
/// The server error code when the operation fails.
/// </param>
/// <param name="ErrorDescription">
/// The server error description when the operation fails.
/// </param>
public sealed record EditResult(
    bool Success,
    long? ObjectId,
    string? GlobalId,
    int? ErrorCode,
    string? ErrorDescription);