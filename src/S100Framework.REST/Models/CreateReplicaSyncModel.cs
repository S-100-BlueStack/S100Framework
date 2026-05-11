namespace S100Framework.REST.Models;

/// <summary>
/// Specifies the sync model requested when creating a replica.
/// </summary>
public enum CreateReplicaSyncModel
{
    /// <summary>
    /// Creates a syncable replica that uses one generation number for the replica.
    /// </summary>
    PerReplica = 0,

    /// <summary>
    /// Creates a syncable replica that uses generation numbers per layer.
    /// </summary>
    PerLayer = 1,

    /// <summary>
    /// Exports data without creating a syncable replica.
    /// </summary>
    None = 2
}