namespace S100Framework.REST.Models;

/// <summary>
/// Specifies the synchronization direction for <c>synchronizeReplica</c>.
/// </summary>
public enum SynchronizeReplicaSyncDirection
{
    /// <summary>
    /// Downloads server-side changes since the supplied generation.
    /// </summary>
    Download = 0,

    /// <summary>
    /// Downloads the current state of the replica data.
    /// </summary>
    Snapshot = 1,

    /// <summary>
    /// Uploads local replica edits to the feature service.
    /// </summary>
    Upload = 2,

    /// <summary>
    /// Uploads local replica edits and downloads server-side changes in the same operation.
    /// </summary>
    Bidirectional = 3
}