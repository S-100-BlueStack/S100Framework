using S100Framework.REST.Models;

namespace S100Framework.REST.Clients;

/// <summary>
/// Provides layer-level append wrapper methods for <see cref="FeatureLayerClient"/>.
/// </summary>
public sealed partial class FeatureLayerClient
{
    /// <inheritdoc />
    public Task<FeatureServiceAppendSubmissionResult> SubmitAppendAsync(
        FeatureLayerAppendEditsRequest request,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(request);

        return _serviceClient.SubmitLayerAppendAsync(_layerId, request, cancellationToken);
    }

    /// <inheritdoc />
    public Task<FeatureServiceAppendSubmissionResult> SubmitAppendAsync(
        FeatureLayerAppendItemRequest request,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(request);

        return _serviceClient.SubmitLayerAppendAsync(_layerId, request, cancellationToken);
    }

    /// <inheritdoc />
    public Task<FeatureServiceAppendSubmissionResult> SubmitAppendAsync(
        FeatureLayerAppendUploadRequest request,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(request);

        return _serviceClient.SubmitLayerAppendAsync(_layerId, request, cancellationToken);
    }

    /// <inheritdoc />
    public Task<FeatureServiceAppendJobStatus> GetAppendStatusAsync(
        Uri statusUrl,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(statusUrl);

        return _serviceClient.GetLayerAppendStatusAsync(statusUrl, cancellationToken);
    }

    /// <inheritdoc />
    public Task<FeatureServiceAppendJobStatus> WaitForAppendCompletionAsync(
        FeatureLayerAppendEditsRequest request,
        AppendWaitOptions? options = null,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(request);

        return _serviceClient.WaitForLayerAppendCompletionAsync(_layerId, request, options, cancellationToken);
    }

    /// <inheritdoc />
    public Task<FeatureServiceAppendJobStatus> WaitForAppendCompletionAsync(
        FeatureLayerAppendItemRequest request,
        AppendWaitOptions? options = null,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(request);

        return _serviceClient.WaitForLayerAppendCompletionAsync(_layerId, request, options, cancellationToken);
    }

    /// <inheritdoc />
    public Task<FeatureServiceAppendJobStatus> WaitForAppendCompletionAsync(
        FeatureLayerAppendUploadRequest request,
        AppendWaitOptions? options = null,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(request);

        return _serviceClient.WaitForLayerAppendCompletionAsync(_layerId, request, options, cancellationToken);
    }

    /// <inheritdoc />
    public Task<FeatureServiceAppendJobStatus> WaitForAppendCompletionAsync(
        Uri statusUrl,
        AppendWaitOptions? options = null,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(statusUrl);

        return _serviceClient.WaitForLayerAppendCompletionAsync(statusUrl, options, cancellationToken);
    }
}