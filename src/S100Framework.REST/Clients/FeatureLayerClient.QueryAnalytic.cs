using S100Framework.REST.Internal.Dto;
using S100Framework.REST.Models;

namespace S100Framework.REST.Clients;

/// <summary>
/// Provides layer-level analytic query wrapper methods for <see cref="FeatureLayerClient" />.
/// </summary>
public sealed partial class FeatureLayerClient
{
    /// <inheritdoc />
    public async Task<QueryAnalyticResult> QueryAnalyticAsync(
        QueryAnalyticRequest request,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(request);

        request.Validate();

        var schema = await GetSchemaAsync(cancellationToken);
        var response = await _serviceClient.QueryAnalyticAsync(_layerId, request, cancellationToken);

        return new QueryAnalyticResult(
            (response.Features ?? new List<EsriFeatureDto>())
                .Select(feature => MapFeature(schema, feature))
                .ToArray(),
            response.ExceededTransferLimit);
    }
}