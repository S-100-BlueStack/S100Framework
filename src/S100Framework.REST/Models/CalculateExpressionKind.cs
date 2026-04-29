namespace S100Framework.REST.Models;

/// <summary>
/// Defines how a <see cref="CalculateExpression" /> supplies the value for a target field.
/// </summary>
public enum CalculateExpressionKind
{
    /// <summary>
    /// Uses the scalar <see cref="CalculateExpression.Value" /> property.
    /// </summary>
    Value = 0,

    /// <summary>
    /// Uses the SQL <see cref="CalculateExpression.SqlExpression" /> property.
    /// </summary>
    SqlExpression = 1
}