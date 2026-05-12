namespace S100Framework.REST.Models;

/// <summary>
/// Represents the result of a state-aware replica creation workflow.
/// </summary>
/// <param name="File">
/// The downloaded replica result file.
/// </param>
/// <param name="InitialState">
/// The initial synchronization state that should be persisted by the consumer.
/// </param>
/// <param name="CreateResult">
/// The completed create replica result used to build the initial state.
/// </param>
public sealed record CreateReplicaStateResult(
    CreateReplicaFileResult File,
    ReplicaSynchronizationState InitialState,
    CreateReplicaResult CreateResult);