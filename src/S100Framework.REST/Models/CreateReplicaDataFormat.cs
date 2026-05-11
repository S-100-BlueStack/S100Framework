namespace S100Framework.REST.Models;

/// <summary>
/// Specifies the response format requested for <c>createReplica</c>.
/// </summary>
public enum CreateReplicaDataFormat
{
    /// <summary>
    /// Returns replica data as JSON.
    /// </summary>
    Json = 0,

    /// <summary>
    /// Returns replica data as a SQLite mobile geodatabase file.
    /// </summary>
    Sqlite = 1
}