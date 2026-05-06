using System.Runtime.CompilerServices;
using System.Text.Json;
using S100Framework.REST.Exceptions;
using S100Framework.REST.Internal.Dto;
using S100Framework.REST.Models;

namespace S100Framework.REST.Clients;

/// <summary>
/// Provides standard feature query operations for <see cref="FeatureLayerClient" />.
/// </summary>
public sealed partial class FeatureLayerClient
{
    /// <inheritdoc />
    public async IAsyncEnumerable<FeatureRecord> QueryAsync(
        FeatureQuery query,
        [EnumeratorCancellation] CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(query);

        ValidateFeatureQueryPaging(query);

        if (query.ReturnEnvelope && !query.ReturnGeometry) {
            throw new InvalidOperationException("ReturnEnvelope requires ReturnGeometry to be true.");
        }

        if (query.ResultOffset is < 0) {
            throw new InvalidOperationException("ResultOffset must be greater than or equal to zero when provided.");
        }

        if (query.ResultRecordCount is <= 0) {
            throw new InvalidOperationException("ResultRecordCount must be greater than zero when provided.");
        }

        var schema = await GetSchemaAsync(cancellationToken);
        var pageSize = ResolvePageSize(query, schema);

        if (query.ReturnEnvelope && !schema.Capabilities.SupportsReturningGeometryEnvelope) {
            throw new InvalidOperationException(
                "ReturnEnvelope requires a layer that supports returning geometry envelopes.");
        }

        if (!schema.Capabilities.SupportsPagination &&
            (query.ResultOffset.HasValue || query.ResultRecordCount.HasValue)) {
            throw new InvalidOperationException(
                "ResultOffset and ResultRecordCount require a layer that supports pagination.");
        }

        if (schema.Capabilities.SupportsPagination) {
            await foreach (var record in QueryWithOffsetPaginationAsync(schema, query, pageSize, cancellationToken)) {
                yield return record;
            }

            yield break;
        }

        if (query.UniqueIds is { Count: > 0 }) {
            await foreach (var record in QueryWithUniqueIdFallbackAsync(schema, query, pageSize, cancellationToken)) {
                yield return record;
            }

            yield break;
        }

        await foreach (var record in QueryWithObjectIdFallbackAsync(schema, query, pageSize, cancellationToken)) {
            yield return record;
        }
    }

    /// <inheritdoc />
    public Task<long> QueryCountAsync(
        FeatureQuery query,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(query);

        return _serviceClient.QueryCountAsync(_layerId, query, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<long>> QueryObjectIdsAsync(
        FeatureQuery query,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(query);

        var response = await _serviceClient.QueryIdsAsync(_layerId, query, cancellationToken);

        return (response.ObjectIds ?? Enumerable.Empty<long?>())
            .Where(static objectId => objectId.HasValue)
            .Select(static objectId => objectId!.Value)
            .ToArray();
    }

    /// <inheritdoc />
    public async Task<FeatureUniqueIdQueryResult> QueryUniqueIdsAsync(
        FeatureQuery query,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(query);

        static IReadOnlyList<string> ReadUniqueIdFieldNames(JsonElement element) {
            return element.ValueKind switch {
                JsonValueKind.Undefined or JsonValueKind.Null => Array.Empty<string>(),
                JsonValueKind.String => element.GetString() is { Length: > 0 } value
                    ? [value]
                    : Array.Empty<string>(),
                JsonValueKind.Array => element.EnumerateArray()
                    .Select(static item => item.ValueKind == JsonValueKind.String ? item.GetString() : null)
                    .Where(static value => !string.IsNullOrWhiteSpace(value))
                    .Select(static value => value!)
                    .ToArray(),
                _ => throw new InvalidOperationException(
                    "The server returned an unsupported payload for uniqueIdFieldNames.")
            };
        }

        static string ReadUniqueIdComponent(JsonElement element) {
            return element.ValueKind switch {
                JsonValueKind.String => element.GetString()
                    ?? throw new InvalidOperationException(
                        "The server returned a null unique ID component."),
                JsonValueKind.Number => element.GetRawText(),
                JsonValueKind.True => bool.TrueString,
                JsonValueKind.False => bool.FalseString,
                _ => throw new InvalidOperationException(
                    "The server returned an unsupported unique ID component value.")
            };
        }

        static IReadOnlyList<FeatureUniqueId> ReadUniqueIds(JsonElement element) {
            if (element.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null) {
                return Array.Empty<FeatureUniqueId>();
            }

            if (element.ValueKind != JsonValueKind.Array) {
                throw new InvalidOperationException("The server returned an unsupported payload for uniqueIds.");
            }

            var result = new List<FeatureUniqueId>();

            foreach (var item in element.EnumerateArray()) {
                if (item.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null) {
                    continue;
                }

                if (item.ValueKind == JsonValueKind.Array) {
                    result.Add(new FeatureUniqueId(
                        item.EnumerateArray()
                            .Select(ReadUniqueIdComponent)
                            .ToArray()));

                    continue;
                }

                result.Add(new FeatureUniqueId([ReadUniqueIdComponent(item)]));
            }

            return result;
        }

        var schema = await GetSchemaAsync(cancellationToken);

        if (!schema.SupportsUniqueIds) {
            throw new FeatureServiceCapabilityException(
                $"Layer '{schema.Name}' ({schema.LayerId}) does not expose unique ID metadata.");
        }

        var response = await _serviceClient.QueryUniqueIdsAsync(_layerId, query, cancellationToken);

        var uniqueIdFieldNames = ReadUniqueIdFieldNames(response.UniqueIdFieldNames);

        if (uniqueIdFieldNames.Count == 0 && schema.UniqueIdInfo is { Fields.Count: > 0 }) {
            uniqueIdFieldNames = schema.UniqueIdInfo.Fields;
        }

        var uniqueIds = ReadUniqueIds(response.UniqueIds);

        return new FeatureUniqueIdQueryResult(
            uniqueIdFieldNames,
            uniqueIds,
            response.ExceededTransferLimit ?? false);
    }

    /// <inheritdoc />
    public Task<FeatureExtent?> QueryExtentAsync(
        FeatureQuery query,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(query);

        return _serviceClient.QueryExtentAsync(_layerId, query, cancellationToken);
    }

    private async IAsyncEnumerable<FeatureRecord> QueryWithOffsetPaginationAsync(
        FeatureLayerSchema schema,
        FeatureQuery query,
        int pageSize,
        [EnumeratorCancellation] CancellationToken cancellationToken) {
        var yielded = 0;
        var offset = query.ResultOffset ?? 0;
        var effectiveLimit = query.Limit.HasValue && query.ResultRecordCount.HasValue
            ? Math.Min(query.Limit.Value, query.ResultRecordCount.Value)
            : query.Limit ?? query.ResultRecordCount;

        while (true) {
            var remaining = effectiveLimit.HasValue
                ? effectiveLimit.Value - yielded
                : int.MaxValue;

            if (remaining <= 0) {
                yield break;
            }

            var requestSize = Math.Min(pageSize, remaining);
            var response = await _serviceClient.QueryFeaturesAsync(
                _layerId,
                query,
                resultOffset: offset,
                resultRecordCount: requestSize,
                objectIds: null,
                uniqueIds: null,
                cancellationToken: cancellationToken);

            var rawFeatures = response.Features ?? new List<EsriFeatureDto>();

            if (rawFeatures.Count == 0) {
                yield break;
            }

            foreach (var feature in EnumerateQueryFeatures(rawFeatures)) {
                yield return MapFeature(schema, feature);

                yielded++;

                if (effectiveLimit.HasValue && yielded >= effectiveLimit.Value) {
                    yield break;
                }
            }

            // Advance by the server-returned array length so malformed null entries cannot cause repeated pages.
            offset += rawFeatures.Count;

            if (rawFeatures.Count < requestSize && response.ExceededTransferLimit != true) {
                yield break;
            }
        }
    }

    private async IAsyncEnumerable<FeatureRecord> QueryWithObjectIdFallbackAsync(
        FeatureLayerSchema schema,
        FeatureQuery query,
        int pageSize,
        [EnumeratorCancellation] CancellationToken cancellationToken) {
        var objectIds = await QueryObjectIdsAsync(query, cancellationToken);

        if (objectIds.Count == 0) {
            yield break;
        }

        var yielded = 0;
        var effectiveLimit = query.Limit;

        for (var offset = 0; offset < objectIds.Count; offset += pageSize) {
            var remaining = effectiveLimit.HasValue
                ? effectiveLimit.Value - yielded
                : int.MaxValue;

            if (remaining <= 0) {
                yield break;
            }

            var requestSize = Math.Min(pageSize, remaining);
            var batch = objectIds
                .Skip(offset)
                .Take(requestSize)
                .ToArray();

            if (batch.Length == 0) {
                yield break;
            }

            var response = await _serviceClient.QueryFeaturesAsync(
                _layerId,
                query,
                resultOffset: null,
                resultRecordCount: null,
                objectIds: batch,
                uniqueIds: null,
                cancellationToken: cancellationToken);

            foreach (var feature in EnumerateQueryFeatures(response.Features)) {
                yield return MapFeature(schema, feature);

                yielded++;

                if (effectiveLimit.HasValue && yielded >= effectiveLimit.Value) {
                    yield break;
                }
            }
        }
    }

    private async IAsyncEnumerable<FeatureRecord> QueryWithUniqueIdFallbackAsync(
        FeatureLayerSchema schema,
        FeatureQuery query,
        int pageSize,
        [EnumeratorCancellation] CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(query.UniqueIds);

        var yielded = 0;
        var effectiveLimit = query.Limit;
        var uniqueIds = query.UniqueIds;

        for (var offset = 0; offset < uniqueIds.Count; offset += pageSize) {
            var remaining = effectiveLimit.HasValue
                ? effectiveLimit.Value - yielded
                : int.MaxValue;

            if (remaining <= 0) {
                yield break;
            }

            var requestSize = Math.Min(pageSize, remaining);
            var batch = uniqueIds
                .Skip(offset)
                .Take(requestSize)
                .ToArray();

            if (batch.Length == 0) {
                yield break;
            }

            var response = await _serviceClient.QueryFeaturesAsync(
                _layerId,
                query,
                resultOffset: null,
                resultRecordCount: null,
                objectIds: null,
                uniqueIds: batch,
                cancellationToken);

            foreach (var feature in EnumerateQueryFeatures(response.Features)) {
                yield return MapFeature(schema, feature);

                yielded++;

                if (effectiveLimit.HasValue && yielded >= effectiveLimit.Value) {
                    yield break;
                }
            }
        }
    }

    private static IEnumerable<EsriFeatureDto> EnumerateQueryFeatures(
        IEnumerable<EsriFeatureDto>? features) {
        if (features is null) {
            yield break;
        }

        foreach (var feature in features) {
            if (feature is null) {
                continue;
            }

            yield return feature;
        }
    }

    private static void ValidateFeatureQueryPaging(FeatureQuery query) {
        if (query.PageSize is <= 0) {
            throw new InvalidOperationException("PageSize must be greater than zero when provided.");
        }

        if (query.Limit is <= 0) {
            throw new InvalidOperationException("Limit must be greater than zero when provided.");
        }
    }

    private int ResolvePageSize(FeatureQuery query, FeatureLayerSchema schema) {
        var candidates = new List<int>();

        if (query.PageSize is > 0) {
            candidates.Add(query.PageSize.Value);
        }

        if (_serviceClient.Options.DefaultPageSize is > 0) {
            candidates.Add(_serviceClient.Options.DefaultPageSize.Value);
        }

        if (schema.MaxRecordCount is > 0) {
            candidates.Add(schema.MaxRecordCount.Value);
        }

        return candidates.Count == 0
            ? 1000
            : candidates.Min();
    }
}