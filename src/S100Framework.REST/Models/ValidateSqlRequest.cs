namespace S100Framework.REST.Models;

/// <summary>
/// Represents a request to validate an SQL expression or WHERE clause against a feature layer.
/// </summary>
public sealed record ValidateSqlRequest
{
    /// <summary>
    /// Gets the SQL expression, WHERE clause, or statement to validate.
    /// </summary>
    public string Sql { get; init; } = string.Empty;

    /// <summary>
    /// Gets the SQL validation type.
    /// </summary>
    public ValidateSqlType SqlType { get; init; } = ValidateSqlType.Where;

    /// <summary>
    /// Validates the SQL validation request before it is sent.
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Sql))
        {
            throw new InvalidOperationException("Sql must be provided.");
        }
    }
}