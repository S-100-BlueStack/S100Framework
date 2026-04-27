namespace S100Framework.REST.Models;

/// <summary>
/// Defines the SQL payload type used by the <c>validateSQL</c> operation.
/// </summary>
public enum ValidateSqlType
{
    /// <summary>
    /// Validates the payload as a WHERE clause.
    /// </summary>
    Where = 0,

    /// <summary>
    /// Validates the payload as an SQL-92 expression.
    /// </summary>
    Expression = 1,

    /// <summary>
    /// Validates the payload as a full SQL-92 statement.
    /// </summary>
    Statement = 2
}