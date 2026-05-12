namespace S100Framework.REST.Models;

/// <summary>
/// Represents the result of a state-aware bidirectional replica synchronization workflow.
/// </summary>
/// <param name="File">
/// The downloaded JSON synchronization result file.
/// </param>
/// <param name="JsonResult">
/// The parsed JSON result file, including upload edit results and generation metadata.
/// </param>
/// <param name="UpdatedState">
/// The updated synchronization state that should be persisted by the consumer.
/// </param>
/// <param name="SynchronizationResult">
/// The completed synchronization result used to update state.
/// </param>
public sealed record SynchronizeReplicaBidirectionalStateResult(
    SynchronizeReplicaFileResult File,
    ReplicaJsonResultFile JsonResult,
    ReplicaSynchronizationState UpdatedState,
    SynchronizeReplicaResult SynchronizationResult);