using S100Framework.REST.Internal.Dto;
using S100Framework.REST.Models;

namespace S100Framework.REST.Clients;

/// <summary>
/// Provides layer-level date-bin query wrapper methods for <see cref="FeatureLayerClient"/>.
/// </summary>
public sealed partial class FeatureLayerClient
{
    /// <inheritdoc />
    public async Task<QueryDateBinsResult> QueryDateBinsAsync(
        QueryDateBinsRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var response = await _serviceClient.QueryDateBinsAsync(_layerId, request, cancellationToken);

        return new QueryDateBinsResult(
            (response.Features ?? Enumerable.Empty<EsriQueryDateBinFeatureDto?>())
                .Where(static feature => feature is not null)
                .Select(static feature => MapQueryDateBinRow(feature!))
                .ToArray(),
            response.ExceededTransferLimit,
            response.GeometryType);
    }

    private static QueryDateBinRow MapQueryDateBinRow(EsriQueryDateBinFeatureDto feature)
    {
        return new QueryDateBinRow(
            ReadAttributes(feature.Attributes),
            feature.Centroid.HasValue &&
            feature.Centroid.Value.ValueKind == System.Text.Json.JsonValueKind.Object
                ? ReadAttributes(feature.Centroid.Value)
                : null);
    }
}