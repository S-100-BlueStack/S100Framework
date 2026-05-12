namespace S100Framework.REST.Models;

/// <summary>
/// Specifies the response format requested for <c>synchronizeReplica</c>.
/// </summary>
public enum SynchronizeReplicaDataFormat
{
    /// <summary>
    /// Returns synchronization changes as JSON.
    /// </summary>
    Json = 0,

    /// <summary>
    /// Returns synchronization changes as a SQLite delta mobile geodatabase file.
    /// </summary>
    Sqlite = 1
}