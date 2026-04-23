namespace S100Framework.REST.Models;

/// <summary>
/// Represents a single full text search expression in a feature query.
/// </summary>
/// <remarks>
/// Exactly one of the following modes must be used:
/// <list type="bullet">
/// <item>
/// <description>
/// A full text expression using <see cref="OnFields"/> and <see cref="SearchTerm"/>.
/// </description>
/// </item>
/// <item>
/// <description>
/// A SQL expression using <see cref="SqlExpression"/>.
/// </description>
/// </item>
/// </list>
/// </remarks>
public sealed record FeatureQueryFullTextExpression
{
    /// <summary>
    /// Gets the layer fields that should be searched.
    /// </summary>
    /// <remarks>
    /// Use <c>*</c> to search all fields that have a full text index.
    /// </remarks>
    public IReadOnlyList<string>? OnFields { get; init; }

    /// <summary>
    /// Gets the term to search for in the full text indexed fields.
    /// </summary>
    public string? SearchTerm { get; init; }

    /// <summary>
    /// Gets the search type to apply for a full text expression.
    /// </summary>
    /// <remarks>
    /// When <see langword="null"/>, the service default behavior is used.
    /// </remarks>
    public FeatureQueryFullTextSearchType? SearchType { get; init; }

    /// <summary>
    /// Gets the operator used within a single full text expression.
    /// </summary>
    /// <remarks>
    /// This is only relevant for <see cref="FeatureQueryFullTextSearchType.Simple"/> searches with multi-word terms.
    /// </remarks>
    public FeatureQueryFullTextOperator? Operator { get; init; }

    /// <summary>
    /// Gets the operator used to combine this expression with the next expression in the array.
    /// </summary>
    public FeatureQueryFullTextSearchOperator? SearchOperator { get; init; }

    /// <summary>
    /// Gets a SQL expression used as a full text search expression.
    /// </summary>
    /// <remarks>
    /// This must not be combined with <see cref="OnFields"/> or <see cref="SearchTerm"/> in the same expression.
    /// </remarks>
    public string? SqlExpression { get; init; }
}