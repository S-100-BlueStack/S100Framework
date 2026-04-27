using S100Framework.REST.Internal.Dto;
using S100Framework.REST.Models;

namespace S100Framework.REST.Clients;

/// <summary>
/// Provides layer-level bin query wrapper methods for <see cref="FeatureLayerClient"/>.
/// </summary>
public sealed partial class FeatureLayerClient
{
    /// <inheritdoc />
    public async Task<QueryBinsResult> QueryBinsAsync(
        QueryBinsRequest request,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(request);

        var response = await _serviceClient.QueryBinsAsync(_layerId, request, cancellationToken);

        return new QueryBinsResult(
            (response.Features ?? new List<EsriQueryBinFeatureDto>())
                .Select(MapQueryBinRow)
                .ToArray(),
            response.ExceededTransferLimit,
            response.StackFieldNames?.ToArray() ?? Array.Empty<string>());
    }

    private static QueryBinRow MapQueryBinRow(EsriQueryBinFeatureDto feature) {
        return new QueryBinRow(
            ReadAttributes(feature.Attributes),
            (feature.StackedAttributes ?? new List<System.Text.Json.JsonElement>())
                .Select(ReadAttributes)
                .ToArray());
    }
}