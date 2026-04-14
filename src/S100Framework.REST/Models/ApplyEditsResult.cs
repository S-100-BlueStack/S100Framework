namespace S100Framework.REST.Models;

public sealed record ApplyEditsResult(
    IReadOnlyList<EditResult> AddResults,
    IReadOnlyList<EditResult> UpdateResults,
    IReadOnlyList<EditResult> DeleteResults);

public sealed record EditResult(
    bool Success,
    long? ObjectId,
    string? GlobalId,
    int? ErrorCode,
    string? ErrorDescription);