namespace S100Framework.REST.Models;

/// <summary>
/// Defines a per-layer generation entry for a <c>synchronizeReplica</c> request.
/// </summary>
public sealed record SynchronizeReplicaSyncLayer
{
    /// <summary>
    /// Gets the layer or table ID.
    /// </summary>
    public required int Id { get; init; }

    /// <summary>
    /// Gets the server generation for the layer.
    /// </summary>
    /// <remarks>
    /// This value is required when <see cref="SyncDirection" /> is <see cref="SynchronizeReplicaSyncDirection.Download" />.
    /// </remarks>
    public long? ServerGen { get; init; }

    /// <summary>
    /// Gets the layer-level synchronization direction.
    /// </summary>
    public SynchronizeReplicaSyncDirection SyncDirection { get; init; } = SynchronizeReplicaSyncDirection.Download;

    /// <summary>
    /// Validates the layer synchronization configuration.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the layer configuration is invalid.
    /// </exception>
    public void Validate() {
        if (Id < 0) {
            throw new InvalidOperationException("Sync layer Id must not be negative.");
        }

        if (!Enum.IsDefined(SyncDirection)) {
            throw new InvalidOperationException("SyncDirection must be a supported synchronizeReplica sync direction.");
        }

        if (ServerGen is < 0) {
            throw new InvalidOperationException("Sync layer ServerGen must not be negative when provided.");
        }

        if (SyncDirection == SynchronizeReplicaSyncDirection.Download && !ServerGen.HasValue) {
            throw new InvalidOperationException(
                "Sync layer ServerGen is required when SyncDirection is Download.");
        }
    }
}