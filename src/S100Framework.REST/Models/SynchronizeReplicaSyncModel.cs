namespace S100Framework.REST.Models;

/// <summary>
/// Specifies the sync model used by a replica synchronization request.
/// </summary>
public enum SynchronizeReplicaSyncModel
{
    /// <summary>
    /// Synchronizes using one generation number for the replica.
    /// </summary>
    PerReplica = 0,

    /// <summary>
    /// Synchronizes using generation numbers per layer.
    /// </summary>
    PerLayer = 1
}