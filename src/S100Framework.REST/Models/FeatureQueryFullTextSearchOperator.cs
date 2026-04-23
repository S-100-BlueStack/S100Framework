namespace S100Framework.REST.Models;

/// <summary>
/// Defines how a full text search expression should be combined with the next expression.
/// </summary>
public enum FeatureQueryFullTextSearchOperator
{
    /// <summary>
    /// Combines this expression with the next one using logical AND.
    /// </summary>
    And = 0,

    /// <summary>
    /// Combines this expression with the next one using logical OR.
    /// </summary>
    Or = 1
}