namespace S100Framework.REST.Models;

/// <summary>
/// Specifies the download-only synchronization direction for <c>synchronizeReplica</c>.
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
    Snapshot = 1
}