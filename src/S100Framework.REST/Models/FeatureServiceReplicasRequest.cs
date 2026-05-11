namespace S100Framework.REST.Models;

/// <summary>
/// Defines optional request parameters for the service-level <c>replicas</c> resource.
/// </summary>
public sealed record FeatureServiceReplicasRequest
{
    /// <summary>
    /// Gets the replica version used to filter returned replicas.
    /// </summary>
    public string? ReplicaVersion { get; init; }

    /// <summary>
    /// Gets a value indicating whether the service should include the replica version in each response item.
    /// </summary>
    public bool ReturnVersion { get; init; }

    /// <summary>
    /// Gets a value indicating whether the service should include the last synchronization date in each response item.
    /// </summary>
    public bool ReturnLastSyncDate { get; init; }

    /// <summary>
    /// Validates the request configuration.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the request configuration is invalid.
    /// </exception>
    public void Validate() {
        if (ReplicaVersion is not null && string.IsNullOrWhiteSpace(ReplicaVersion)) {
            throw new InvalidOperationException("ReplicaVersion must not be empty or whitespace when provided.");
        }
    }
}