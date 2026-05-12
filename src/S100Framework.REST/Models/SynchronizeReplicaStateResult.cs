namespace S100Framework.REST.Models;

/// <summary>
/// Represents the result of a state-aware replica synchronization workflow.
/// </summary>
/// <param name="File">
/// The downloaded synchronization result file.
/// </param>
/// <param name="UpdatedState">
/// The updated synchronization state that should be persisted by the consumer.
/// </param>
/// <param name="SynchronizationResult">
/// The completed synchronization result used to update the state.
/// </param>
public sealed record SynchronizeReplicaStateResult(
    SynchronizeReplicaFileResult File,
    ReplicaSynchronizationState UpdatedState,
    SynchronizeReplicaResult SynchronizationResult);