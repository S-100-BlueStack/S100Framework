namespace S100Framework.REST.Models;

/// <summary>
/// Defines how domain dictionaries are included in a <c>queryContingentValues</c> response.
/// </summary>
public enum QueryContingentValuesDomainDictionaries
{
    /// <summary>
    /// Does not include domain dictionaries.
    /// </summary>
    None = 0,

    /// <summary>
    /// Includes complete domain dictionaries.
    /// </summary>
    Complete = 1,

    /// <summary>
    /// Includes only domain dictionary entries referenced by contingencies.
    /// </summary>
    Trimmed = 2
}