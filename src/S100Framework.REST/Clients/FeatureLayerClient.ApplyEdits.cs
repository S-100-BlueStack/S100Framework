using S100Framework.REST.Exceptions;
using S100Framework.REST.Models;

namespace S100Framework.REST.Clients;

/// <summary>
/// Provides layer-level <c>applyEdits</c> wrapper methods for <see cref="FeatureLayerClient" />.
/// </summary>
public sealed partial class FeatureLayerClient
{
    /// <inheritdoc />
    public Task<ApplyEditsResult> ApplyEditsAsync(
        FeatureEdits edits,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(edits);

        return _serviceClient.ApplyEditsAsync(_layerId, edits, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<EditResult>> AddFeaturesAsync(
        IReadOnlyList<EditableFeature> features,
        FeatureEditOptions? options = null,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(features);

        var effectiveOptions = options ?? new FeatureEditOptions();

        var result = await ApplyEditsAsync(
            new FeatureEdits {
                Adds = features,
                RollbackOnFailure = effectiveOptions.RollbackOnFailure,
                UseGlobalIds = effectiveOptions.UseGlobalIds
            },
            cancellationToken);

        return result.AddResults;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<EditResult>> UpdateFeaturesAsync(
        IReadOnlyList<EditableFeature> features,
        FeatureEditOptions? options = null,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(features);

        var effectiveOptions = options ?? new FeatureEditOptions();

        var result = await ApplyEditsAsync(
            new FeatureEdits {
                Updates = features,
                RollbackOnFailure = effectiveOptions.RollbackOnFailure,
                UseGlobalIds = effectiveOptions.UseGlobalIds
            },
            cancellationToken);

        return result.UpdateResults;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<EditResult>> DeleteFeaturesAsync(
        IReadOnlyList<long> objectIds,
        FeatureEditOptions? options = null,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(objectIds);

        var effectiveOptions = options ?? new FeatureEditOptions();

        var result = await ApplyEditsAsync(
            new FeatureEdits {
                Deletes = objectIds,
                RollbackOnFailure = effectiveOptions.RollbackOnFailure,
                UseGlobalIds = effectiveOptions.UseGlobalIds
            },
            cancellationToken);

        return result.DeleteResults;
    }

    /// <inheritdoc />
    public async Task<ApplyEditsSubmissionResult> SubmitApplyEditsAsync(
        FeatureEdits edits,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(edits);

        edits.Validate();

        var schema = await GetSchemaAsync(cancellationToken);

        if (!schema.Capabilities.SupportsAsyncApplyEdits) {
            throw new FeatureServiceCapabilityException(
                $"Layer '{schema.Name}' ({schema.LayerId}) does not support asynchronous applyEdits.");
        }

        return await _serviceClient.SubmitApplyEditsAsync(
            _layerId,
            edits,
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<ApplyEditsJobStatus> GetApplyEditsStatusAsync(
        Uri statusUrl,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(statusUrl);

        return _serviceClient.GetLayerApplyEditsStatusAsync(
            statusUrl,
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<ApplyEditsResult> GetApplyEditsResultAsync(
        Uri resultUrl,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(resultUrl);

        return _serviceClient.GetLayerApplyEditsResultAsync(
            resultUrl,
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<ApplyEditsResult> WaitForApplyEditsCompletionAsync(
        FeatureEdits edits,
        ApplyEditsWaitOptions? options = null,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(edits);

        return _serviceClient.WaitForLayerApplyEditsCompletionAsync(
            _layerId,
            edits,
            options,
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<ApplyEditsResult> WaitForApplyEditsCompletionAsync(
        Uri statusUrl,
        ApplyEditsWaitOptions? options = null,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(statusUrl);

        return _serviceClient.WaitForLayerApplyEditsCompletionAsync(
            statusUrl,
            options,
            cancellationToken);
    }
}