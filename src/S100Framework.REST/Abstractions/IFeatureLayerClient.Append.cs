using S100Framework.REST.Models;

namespace S100Framework.REST.Abstractions;

/// <summary>
/// Defines optional layer-level append operations for a feature layer endpoint.
/// </summary>
public partial interface IFeatureLayerClient
{
    /// <summary>
    /// Submits a layer-level <c>append</c> request using the literal <c>edits</c> source.
    /// </summary>
    /// <param name="request">The append request to submit.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The append submission response.</returns>
    /// <exception cref="NotSupportedException">
    /// Thrown when the concrete layer client implementation does not support append operations.
    /// </exception>
    Task<FeatureServiceAppendSubmissionResult> SubmitAppendAsync(
        FeatureLayerAppendEditsRequest request,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This feature layer client does not support append operations.");

    /// <summary>
    /// Submits a layer-level <c>append</c> request using a portal item as the source.
    /// </summary>
    /// <param name="request">The append request to submit.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The append submission response.</returns>
    /// <exception cref="NotSupportedException">
    /// Thrown when the concrete layer client implementation does not support append operations.
    /// </exception>
    Task<FeatureServiceAppendSubmissionResult> SubmitAppendAsync(
        FeatureLayerAppendItemRequest request,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This feature layer client does not support append operations.");

    /// <summary>
    /// Submits a layer-level <c>append</c> request using a server upload item as the source.
    /// </summary>
    /// <param name="request">The append request to submit.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The append submission response.</returns>
    /// <exception cref="NotSupportedException">
    /// Thrown when the concrete layer client implementation does not support append operations.
    /// </exception>
    Task<FeatureServiceAppendSubmissionResult> SubmitAppendAsync(
        FeatureLayerAppendUploadRequest request,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This feature layer client does not support append operations.");

    /// <summary>
    /// Gets the current status of a previously submitted layer-level <c>append</c> job.
    /// </summary>
    /// <param name="statusUrl">The append job status URL.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The current append job status.</returns>
    /// <exception cref="NotSupportedException">
    /// Thrown when the concrete layer client implementation does not support append operations.
    /// </exception>
    Task<FeatureServiceAppendJobStatus> GetAppendStatusAsync(
        Uri statusUrl,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This feature layer client does not support append operations.");

    /// <summary>
    /// Submits a layer-level <c>append</c> request using literal <c>edits</c> and waits for completion.
    /// </summary>
    /// <param name="request">The append request to submit.</param>
    /// <param name="options">Polling options. When <see langword="null"/>, default polling behavior is used.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The terminal append job status.</returns>
    /// <exception cref="NotSupportedException">
    /// Thrown when the concrete layer client implementation does not support append operations.
    /// </exception>
    Task<FeatureServiceAppendJobStatus> WaitForAppendCompletionAsync(
        FeatureLayerAppendEditsRequest request,
        AppendWaitOptions? options = null,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This feature layer client does not support append operations.");

    /// <summary>
    /// Submits a layer-level <c>append</c> request using a portal item and waits for completion.
    /// </summary>
    /// <param name="request">The append request to submit.</param>
    /// <param name="options">Polling options. When <see langword="null"/>, default polling behavior is used.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The terminal append job status.</returns>
    /// <exception cref="NotSupportedException">
    /// Thrown when the concrete layer client implementation does not support append operations.
    /// </exception>
    Task<FeatureServiceAppendJobStatus> WaitForAppendCompletionAsync(
        FeatureLayerAppendItemRequest request,
        AppendWaitOptions? options = null,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This feature layer client does not support append operations.");

    /// <summary>
    /// Submits a layer-level <c>append</c> request using a server upload item and waits for completion.
    /// </summary>
    /// <param name="request">The append request to submit.</param>
    /// <param name="options">Polling options. When <see langword="null"/>, default polling behavior is used.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The terminal append job status.</returns>
    /// <exception cref="NotSupportedException">
    /// Thrown when the concrete layer client implementation does not support append operations.
    /// </exception>
    Task<FeatureServiceAppendJobStatus> WaitForAppendCompletionAsync(
        FeatureLayerAppendUploadRequest request,
        AppendWaitOptions? options = null,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This feature layer client does not support append operations.");

    /// <summary>
    /// Polls an existing layer-level <c>append</c> job until it reaches a terminal state.
    /// </summary>
    /// <param name="statusUrl">The append job status URL.</param>
    /// <param name="options">Polling options. When <see langword="null"/>, default polling behavior is used.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The terminal append job status.</returns>
    /// <exception cref="NotSupportedException">
    /// Thrown when the concrete layer client implementation does not support append operations.
    /// </exception>
    Task<FeatureServiceAppendJobStatus> WaitForAppendCompletionAsync(
        Uri statusUrl,
        AppendWaitOptions? options = null,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This feature layer client does not support append operations.");
}