using S100Framework.REST.Models;

namespace S100Framework.REST.Abstractions;

public interface IFeatureServiceClient
{
    Task<FeatureServiceMetadata> GetMetadataAsync(CancellationToken cancellationToken = default);

    IFeatureLayerClient GetLayerClient(int layerId);

    Task<FeatureServiceApplyEditsResult> ApplyEditsAsync(
        FeatureServiceEdits edits,
        CancellationToken cancellationToken = default);

    Task<IFeatureLayerClient> GetLayerClientAsync(
    string layerName,
    CancellationToken cancellationToken = default);

    Task<ExtractChangesResult> ExtractChangesAsync(
        ExtractChangesRequest request,
        CancellationToken cancellationToken = default);
}