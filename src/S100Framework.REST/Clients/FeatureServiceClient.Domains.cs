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
                ["layers"] = JsonSerializer.Serialize(layerIds)
            });

        var dto = await GetAsync<EsriQueryDomainsResponseDto>(uri, cancellationToken);

        return (dto.Domains ?? new List<EsriDomainDto>())
            .Select(MapDomain)
            .ToArray();
    }

    private static FeatureServiceDomain MapDomain(EsriDomainDto dto) {
        ArgumentNullException.ThrowIfNull(dto);

        FeatureServiceDomainRange? range = null;

        if (dto.Range.ValueKind == JsonValueKind.Array &&
            dto.Range.GetArrayLength() >= 2) {
            range = new FeatureServiceDomainRange(
                ReadScalar(dto.Range[0]),
                ReadScalar(dto.Range[1]));
        }

        var codedValues = (dto.CodedValues ?? new List<EsriCodedValueDto>())
            .Select(static codedValue => new FeatureServiceCodedValue(
                codedValue.Name ?? string.Empty,
                ReadScalar(codedValue.Code)))
            .ToArray();

        return new FeatureServiceDomain(
            dto.Type ?? "unknown",
            dto.Name,
            dto.FieldType,
            dto.MergePolicy,
            dto.SplitPolicy,
            range,
            codedValues);
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
            JsonValueKind.Array => element.GetRawText(),
            JsonValueKind.Object => element.GetRawText(),
            _ => element.GetRawText()
        };
    }
}