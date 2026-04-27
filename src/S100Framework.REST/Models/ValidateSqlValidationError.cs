namespace S100Framework.REST.Models;

/// <summary>
/// Represents an individual SQL validation error returned by the service.
/// </summary>
/// <param name="ErrorCode">The service-specific validation error code.</param>
/// <param name="Description">The validation error description.</param>
public sealed record ValidateSqlValidationError(
    int? ErrorCode,
    string? Description);