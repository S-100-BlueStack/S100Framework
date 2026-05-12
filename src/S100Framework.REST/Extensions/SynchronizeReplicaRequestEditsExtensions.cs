using S100Framework.REST.Models;

namespace S100Framework.REST.Extensions;

/// <summary>
/// Provides helpers for applying structured replica edit payloads to synchronize replica requests.
/// </summary>
public static class SynchronizeReplicaRequestEditsExtensions
{
    /// <summary>
    /// Returns a new synchronize replica request with <see cref="SynchronizeReplicaRequest.EditsJson" />
    /// populated from a structured edit payload.
    /// </summary>
    /// <param name="request">
    /// The synchronize replica request to copy.
    /// </param>
    /// <param name="edits">
    /// The structured edit payload.
    /// </param>
    /// <param name="options">
    /// Optional JSON serialization options for the edit payload.
    /// </param>
    /// <returns>
    /// A new request with the serialized edit payload assigned to <see cref="SynchronizeReplicaRequest.EditsJson" />.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="request" /> or <paramref name="edits" /> is <see langword="null" />.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the request already uses <see cref="SynchronizeReplicaRequest.EditsUploadId" />.
    /// </exception>
    public static SynchronizeReplicaRequest WithEdits(
        this SynchronizeReplicaRequest request,
        ReplicaEdits edits,
        ReplicaEditsJsonOptions? options = null) {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(edits);

        if (!string.IsNullOrWhiteSpace(request.EditsUploadId)) {
            throw new InvalidOperationException(
                "EditsJson cannot be generated when EditsUploadId is already provided.");
        }

        return request with {
            EditsJson = edits.ToJson(options)
        };
    }
}