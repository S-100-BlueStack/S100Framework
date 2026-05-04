using S100Framework.REST.Internal.Dto;
using S100Framework.REST.Models;

namespace S100Framework.REST.Clients;

/// <summary>
/// Provides layer-level bin query wrapper methods for <see cref="FeatureLayerClient" />.
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
            (response.Features ?? Enumerable.Empty<EsriQueryBinFeatureDto?>())
                .Where(static feature => feature is not null)
                .Select(static feature => MapQueryBinRow(feature!))
                .ToArray(),
            response.ExceededTransferLimit,
            (response.StackFieldNames ?? Enumerable.Empty<string?>())
                .Where(static fieldName => !string.IsNullOrWhiteSpace(fieldName))
                .Select(static fieldName => fieldName!)
                .ToArray());
    }

    private static QueryBinRow MapQueryBinRow(EsriQueryBinFeatureDto feature) {
        return new QueryBinRow(
            ReadAttributes(feature.Attributes),
            (feature.StackedAttributes ?? Enumerable.Empty<System.Text.Json.JsonElement?>())
                .Where(static stackedAttributes => stackedAttributes.HasValue)
                .Select(static stackedAttributes => ReadAttributes(stackedAttributes!.Value))
                .ToArray());
    }
}