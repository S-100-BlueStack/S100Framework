using S100Framework.REST.Models;

namespace S100Framework.REST.Abstractions;

public interface IFeatureServiceClient
{
    Task<FeatureServiceMetadata> GetMetadataAsync(CancellationToken cancellationToken = default);

    IFeatureLayerClient GetLayerClient(int layerId);

    Task<FeatureServiceApplyEditsResult> ApplyEditsAsync(
        FeatureServiceEdits edits,
        CancellationToken cancellationToken = default);
}