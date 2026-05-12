namespace S100Framework.REST.Models;

/// <summary>
/// Specifies how <c>synchronizeReplica</c> should return synchronization content.
/// </summary>
public enum SynchronizeReplicaTransportType
{
    /// <summary>
    /// Returns a URL that points to the generated synchronization content.
    /// </summary>
    Url = 0,

    /// <summary>
    /// Returns JSON synchronization content directly in the response payload.
    /// </summary>
    Embedded = 1
}