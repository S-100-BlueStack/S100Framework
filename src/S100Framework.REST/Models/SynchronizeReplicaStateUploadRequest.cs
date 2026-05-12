using System.Text.Json;

namespace S100Framework.REST.Models;

/// <summary>
/// Defines options for a state-aware upload-only replica synchronization workflow.
/// </summary>
public sealed record SynchronizeReplicaStateUploadRequest
{
    /// <summary>
    /// Gets structured local edits to upload.
    /// </summary>
    /// <remarks>
    /// Exactly one of <see cref="Edits" />, <see cref="EditsJson" />, or <see cref="EditsUploadId" /> must be provided.
    /// </remarks>
    public ReplicaEdits? Edits { get; init; }

    /// <summary>
    /// Gets raw Esri <c>edits</c> JSON to upload.
    /// </summary>
    /// <remarks>
    /// Exactly one of <see cref="Edits" />, <see cref="EditsJson" />, or <see cref="EditsUploadId" /> must be provided.
    /// </remarks>
    public string? EditsJson { get; init; }

    /// <summary>
    /// Gets an uploaded edits payload ID.
    /// </summary>
    /// <remarks>
    /// Exactly one of <see cref="Edits" />, <see cref="EditsJson" />, or <see cref="EditsUploadId" /> must be provided.
    /// </remarks>
    public string? EditsUploadId { get; init; }

    /// <summary>
    /// Gets JSON output options used when <see cref="Edits" /> is provided.
    /// </summary>
    public ReplicaEditsJsonOptions? EditsJsonOptions { get; init; }

    /// <summary>
    /// Gets a value indicating whether the request should be submitted asynchronously.
    /// </summary>
    public bool IsAsync { get; init; }

    /// <summary>
    /// Gets a value indicating whether attachment content should be referenced by URL instead of embedded.
    /// </summary>
    public bool ReturnAttachmentsDataByUrl { get; init; }

    /// <summary>
    /// Gets a value indicating whether uploaded edits should be rolled back if any edit fails.
    /// </summary>
    public bool RollbackOnFailure { get; init; }

    /// <summary>
    /// Gets a value indicating whether the service should return object IDs for added features.
    /// </summary>
    public bool ReturnIdsForAdds { get; init; }

    /// <summary>
    /// Gets a value indicating whether the workflow should throw when the downloaded JSON result file contains failed edit results.
    /// </summary>
    /// <remarks>
    /// When enabled, <c>SynchronizeReplicaStateUploadAsync</c> throws <see cref="S100Framework.REST.Exceptions.ReplicaEditResultsException" />
    /// after parsing the result file.
    /// </remarks>
    public bool ThrowOnEditErrors { get; init; }

    /// <summary>
    /// Gets polling options used when <see cref="IsAsync" /> is <see langword="true" />.
    /// </summary>
    public ReplicaPollingOptions? PollingOptions { get; init; }

    /// <summary>
    /// Validates the upload-only synchronization options.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the request configuration is incomplete or internally inconsistent.
    /// </exception>
    public void Validate() {
        var payloadCount = 0;

        if (Edits is not null) {
            payloadCount++;
            Edits.Validate();
        }

        if (EditsJson is not null) {
            payloadCount++;

            if (string.IsNullOrWhiteSpace(EditsJson)) {
                throw new InvalidOperationException("EditsJson must not be empty or whitespace when provided.");
            }

            ValidateEditsJson();
        }

        if (EditsUploadId is not null) {
            payloadCount++;

            if (string.IsNullOrWhiteSpace(EditsUploadId)) {
                throw new InvalidOperationException("EditsUploadId must not be empty or whitespace when provided.");
            }
        }

        if (payloadCount == 0) {
            throw new InvalidOperationException(
                "Upload-only replica synchronization requires Edits, EditsJson, or EditsUploadId.");
        }

        if (payloadCount > 1) {
            throw new InvalidOperationException(
                "Only one of Edits, EditsJson, or EditsUploadId can be provided.");
        }

        PollingOptions?.Validate();
    }

    private void ValidateEditsJson() {
        try {
            using var document = JsonDocument.Parse(EditsJson!);

            if (document.RootElement.ValueKind is not JsonValueKind.Object and not JsonValueKind.Array) {
                throw new InvalidOperationException(
                    "EditsJson must contain a JSON object or array.");
            }
        }
        catch (JsonException exception) {
            throw new InvalidOperationException(
                "EditsJson must contain valid JSON.",
                exception);
        }
    }
}