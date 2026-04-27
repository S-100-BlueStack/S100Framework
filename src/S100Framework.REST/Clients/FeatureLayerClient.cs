using S100Framework.REST.Abstractions;
using S100Framework.REST.Models;

namespace S100Framework.REST.Clients;

/// <summary>
/// Provides operations for querying, editing, and downloading attachments from a single feature layer or table.
/// </summary>
public sealed partial class FeatureLayerClient : IFeatureLayerClient
{
    private readonly FeatureServiceClient _serviceClient;
    private readonly int _layerId;
    private readonly SemaphoreSlim _schemaLock = new(1, 1);

    private FeatureLayerSchema? _schema;

    internal FeatureLayerClient(FeatureServiceClient serviceClient, int layerId) {
        _serviceClient = serviceClient;
        _layerId = layerId;
    }

    /// <inheritdoc />
    public async Task<FeatureLayerSchema> GetSchemaAsync(CancellationToken cancellationToken = default) {
        if (_schema is not null) {
            return _schema;
        }

        await _schemaLock.WaitAsync(cancellationToken);

        try {
            if (_schema is not null) {
                return _schema;
            }

            _schema = await _serviceClient.GetLayerSchemaAsync(_layerId, cancellationToken);
            return _schema;
        }
        finally {
            _schemaLock.Release();
        }
    }
}