namespace S100Framework.REST.Models;

/// <summary>
/// Specifies how <c>createReplica</c> should return replica content.
/// </summary>
public enum CreateReplicaTransportType
{
    /// <summary>
    /// Returns a URL that points to the generated replica content.
    /// </summary>
    Url = 0,

    /// <summary>
    /// Returns JSON replica content directly in the response payload.
    /// </summary>
    Embedded = 1
}