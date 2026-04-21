namespace S100Framework.REST.Models;

/// <summary>
/// Specifies the response format requested for <c>extractChanges</c>.
/// </summary>
public enum ExtractChangesDataFormat
{
    /// <summary>
    /// Returns the result as JSON in the response payload.
    /// </summary>
    Json = 0,

    /// <summary>
    /// Returns the result as a SQLite file that must be downloaded separately.
    /// </summary>
    Sqlite = 1
}