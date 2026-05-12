namespace S100Framework.REST.Models;

/// <summary>
/// Represents the result of a state-aware upload-only replica synchronization workflow.
/// </summary>
/// <param name="File">
/// The downloaded JSON synchronization result file.
/// </param>
/// <param name="JsonResult">
/// The parsed JSON result file, including upload edit results.
/// </param>
/// <param name="State">
/// The unchanged synchronization state represented by the upload-only workflow.
/// </param>
/// <param name="SynchronizationResult">
/// The completed synchronization result returned by the service.
/// </param>
public sealed record SynchronizeReplicaUploadStateResult(
    SynchronizeReplicaFileResult File,
    ReplicaJsonResultFile JsonResult,
    ReplicaSynchronizationState State,
    SynchronizeReplicaResult SynchronizationResult);