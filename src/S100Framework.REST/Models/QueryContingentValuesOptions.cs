namespace S100Framework.REST.Models;

/// <summary>
/// Represents optional layer-level contingent values query options.
/// </summary>
public sealed record QueryContingentValuesOptions
{
    /// <summary>
    /// Gets a value indicating whether hosted feature services should return compact contingent value codes.
    /// </summary>
    public bool? CompactFormat { get; init; }

    /// <summary>
    /// Gets how hosted feature services should include domain dictionaries.
    /// </summary>
    public QueryContingentValuesDomainDictionaries? DomainDictionaries { get; init; }
}