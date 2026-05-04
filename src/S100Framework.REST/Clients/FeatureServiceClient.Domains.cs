using System.Text.Json;
using S100Framework.REST.Exceptions;
using S100Framework.REST.Internal.Dto;
using S100Framework.REST.Internal.Http;
using S100Framework.REST.Models;

namespace S100Framework.REST.Clients;

/// <summary>
/// Provides domain-query operations for a feature service.
/// </summary>
public sealed partial class FeatureServiceClient
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<FeatureServiceDomain>> QueryDomainsAsync(
        IReadOnlyList<int> layerIds,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(layerIds);

        if (layerIds.Count == 0) {
            throw new InvalidOperationException("At least one layer ID must be provided.");
        }

        if (layerIds.Any(static layerId => layerId < 0)) {
            throw new InvalidOperationException("Layer IDs must not contain negative values.");
        }

        if (layerIds.Distinct().Count() != layerIds.Count) {
            throw new InvalidOperationException("Layer IDs must not contain duplicate values.");
        }

        var metadata = await GetMetadataAsync(cancellationToken);

        if (!metadata.Capabilities.SupportsQueryDomains) {
            throw new FeatureServiceCapabilityException(
                "The feature service does not advertise queryDomains support.");
        }

        var uri = UriUtility.WithQuery(
            UriUtility.AppendPath(_serviceUri, "queryDomains"),
            new Dictionary<string, string?> {
                ["f"] = "json",
                ["layers"] = JsonSerializer.Serialize(layerIds, JsonOptions)
            });

        var dto = await GetAsync<EsriQueryDomainsResponseDto>(uri, cancellationToken);

        return (dto.Domains ?? Enumerable.Empty<EsriDomainDto?>())
            .Where(static domain => domain is not null)
            .Select(static domain => MapDomain(domain!))
            .ToArray();
    }

    private static FeatureServiceDomain MapDomain(EsriDomainDto dto) {
        var codedValues = (dto.CodedValues ?? Enumerable.Empty<EsriCodedValueDto?>())
            .Where(static codedValue => codedValue is not null)
            .Select(static codedValue => new FeatureServiceCodedValue(
                codedValue!.Name ?? string.Empty,
                ReadScalar(codedValue.Code)))
            .ToArray();

        return new FeatureServiceDomain(
            dto.Type ?? "unknown",
            dto.Name,
            dto.FieldType,
            dto.MergePolicy,
            dto.SplitPolicy,
            MapDomainRange(dto.Range),
            codedValues);
    }

    private static FeatureServiceDomainRange? MapDomainRange(JsonElement range) {
        if (range.ValueKind != JsonValueKind.Array || range.GetArrayLength() < 2) {
            return null;
        }

        return new FeatureServiceDomainRange(
            ReadScalar(range[0]),
            ReadScalar(range[1]));
    }

    private static object? ReadScalar(JsonElement element) {
        return element.ValueKind switch {
            JsonValueKind.Undefined => null,
            JsonValueKind.Null => null,
            JsonValueKind.String => element.GetString(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number when element.TryGetInt64(out var int64Value) => int64Value,
            JsonValueKind.Number when element.TryGetDouble(out var doubleValue) => doubleValue,
            JsonValueKind.Array => JsonSerializer.Serialize(element),
            JsonValueKind.Object => JsonSerializer.Serialize(element),
            _ => element.GetRawText()
        };
    }
}