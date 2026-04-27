namespace S100Framework.REST.Models;

/// <summary>
/// Represents the result of a feature layer <c>validateSQL</c> operation.
/// </summary>
/// <param name="IsValidSql">Indicates whether the SQL payload is valid.</param>
/// <param name="ValidationErrors">The validation errors returned by the service.</param>
public sealed record ValidateSqlResult(
    bool IsValidSql,
    IReadOnlyList<ValidateSqlValidationError> ValidationErrors);