using S100Framework.REST.Models;

namespace S100Framework.REST.Abstractions;

public interface IFeatureLayerClient
{
    Task<FeatureLayerSchema> GetSchemaAsync(CancellationToken cancellationToken = default);

    IAsyncEnumerable<FeatureRecord> QueryAsync(
        FeatureQuery query,
        CancellationToken cancellationToken = default);
}