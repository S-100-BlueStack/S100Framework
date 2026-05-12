namespace S100Framework.REST.Models;

/// <summary>
/// Identifies the edit operation type represented by a replica edit result.
/// </summary>
public enum ReplicaEditOperation
{
    /// <summary>
    /// The edit result belongs to an add operation.
    /// </summary>
    Add = 0,

    /// <summary>
    /// The edit result belongs to an update operation.
    /// </summary>
    Update = 1,

    /// <summary>
    /// The edit result belongs to a delete operation.
    /// </summary>
    Delete = 2
}