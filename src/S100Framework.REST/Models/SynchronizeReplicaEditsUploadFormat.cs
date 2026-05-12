namespace S100Framework.REST.Models;

/// <summary>
/// Specifies the uploaded edits payload format used by <c>synchronizeReplica</c>.
/// </summary>
public enum SynchronizeReplicaEditsUploadFormat
{
    /// <summary>
    /// The uploaded edits payload is a SQLite delta mobile geodatabase.
    /// </summary>
    Sqlite = 0
}