namespace S100Framework.REST.Models;

/// <summary>
/// Defines a request for unregistering a feature service replica.
/// </summary>
public sealed record UnregisterReplicaRequest
{
    /// <summary>
    /// Gets the replica ID to unregister.
    /// </summary>
    /// <remarks>
    /// ArcGIS Enterprise 10.5.1 and later supports <c>*</c> to unregister all replicas accessible to the caller.
    /// </remarks>
    public required string ReplicaId { get; init; }

    /// <summary>
    /// Validates the request configuration.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the request configuration is invalid.
    /// </exception>
    public void Validate() {
        if (string.IsNullOrWhiteSpace(ReplicaId)) {
            throw new InvalidOperationException("ReplicaId must be provided.");
        }
    }
}