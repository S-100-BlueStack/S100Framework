namespace S100Framework.REST.Models;

/// <summary>
/// Defines the operator used within a single full text search expression.
/// </summary>
public enum FeatureQueryFullTextOperator
{
    /// <summary>
    /// Matches when all words in the search term are present.
    /// </summary>
    And = 0,

    /// <summary>
    /// Matches when any word in the search term is present.
    /// </summary>
    Or = 1,

    /// <summary>
    /// Matches the first word while excluding subsequent words.
    /// </summary>
    Not = 2
}