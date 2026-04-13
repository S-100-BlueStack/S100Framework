using NetTopologySuite.Geometries;
using S100Framework.REST.Abstractions;
using S100Framework.REST.Internal.Dto;
using S100Framework.REST.Internal.Geometry;
using S100Framework.REST.Models;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace S100Framework.REST.Clients;

public sealed class FeatureLayerClient : IFeatureLayerClient
{
    private readonly FeatureServiceClient _serviceClient;
    private readonly int _layerId;
    private Task<FeatureLayerSchema>? _schemaTask;

    internal FeatureLayerClient(FeatureServiceClient serviceClient, int layerId) {
        _serviceClient = serviceClient;
        _layerId = layerId;
    }

    public Task<FeatureLayerSchema> GetSchemaAsync(CancellationToken cancellationToken = default) {
        _schemaTask ??= _serviceClient.GetLayerSchemaAsync(_layerId, cancellationToken);
        return _schemaTask;
    }

    public async IAsyncEnumerable<FeatureRecord> QueryAsync(
        FeatureQuery query,
        [EnumeratorCancellation] CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(query);

        var schema = await GetSchemaAsync(cancellationToken);
        var pageSize = ResolvePageSize(query, schema);

        if (schema.SupportsPagination) {
            await foreach (var record in QueryWithOffsetPaginationAsync(
                schema,
                query,
                pageSize,
                cancellationToken)) {
                yield return record;
            }

            yield break;
        }

        await foreach (var record in QueryWithObjectIdFallbackAsync(
            schema,
            query,
            pageSize,
            cancellationToken)) {
            yield return record;
        }
    }

    private async IAsyncEnumerable<FeatureRecord> QueryWithOffsetPaginationAsync(
        FeatureLayerSchema schema,
        FeatureQuery query,
        int pageSize,
        [EnumeratorCancellation] CancellationToken cancellationToken) {
        var yielded = 0;
        var offset = 0;

        while (true) {
            var remaining = query.Limit.HasValue
                ? query.Limit.Value - yielded
                : int.MaxValue;

            if (remaining <= 0) {
                yield break;
            }

            var requestSize = Math.Min(pageSize, remaining);

            var response = await _serviceClient.QueryFeaturesAsync(
                _layerId,
                query,
                offset,
                requestSize,
                objectIds: null,
                cancellationToken);

            var features = response.Features ?? [];

            if (features.Count == 0) {
                yield break;
            }

            foreach (var feature in features) {
                yield return MapFeature(schema, feature);
                yielded++;

                if (query.Limit.HasValue && yielded >= query.Limit.Value) {
                    yield break;
                }
            }

            offset += features.Count;

            if (features.Count < requestSize && response.ExceededTransferLimit != true) {
                yield break;
            }
        }
    }

    private async IAsyncEnumerable<FeatureRecord> QueryWithObjectIdFallbackAsync(
        FeatureLayerSchema schema,
        FeatureQuery query,
        int pageSize,
        [EnumeratorCancellation] CancellationToken cancellationToken) {
        var idsResponse = await _serviceClient.QueryIdsAsync(_layerId, query, cancellationToken);
        var objectIds = idsResponse.ObjectIds ?? [];

        if (objectIds.Count == 0) {
            yield break;
        }

        var effectiveIds = query.Limit.HasValue
            ? objectIds.Take(query.Limit.Value).ToArray()
            : objectIds.ToArray();

        foreach (var batch in Chunk(effectiveIds, pageSize)) {
            var response = await _serviceClient.QueryFeaturesAsync(
                _layerId,
                query,
                resultOffset: null,
                resultRecordCount: null,
                objectIds: batch,
                cancellationToken);

            foreach (var feature in response.Features ?? []) {
                yield return MapFeature(schema, feature);
            }
        }
    }

    private FeatureRecord MapFeature(FeatureLayerSchema schema, EsriFeatureDto feature) {
        var attributes = ReadAttributes(feature.Attributes);
        var geometry = queryGeometry(feature.Geometry, schema);

        long? objectId = null;

        if (!string.IsNullOrWhiteSpace(schema.ObjectIdFieldName) &&
            attributes.TryGetValue(schema.ObjectIdFieldName, out var rawObjectId)) {
            objectId = ConvertToInt64(rawObjectId);
        }

        return new FeatureRecord(geometry, attributes, objectId);

        Geometry? queryGeometry(JsonElement geometryElement, FeatureLayerSchema layerSchema) {
            if (!feature.Geometry.ValueKind.Equals(JsonValueKind.Undefined) &&
                geometryElement.ValueKind != JsonValueKind.Null) {
                return EsriGeometryReader.Read(
                    geometryElement,
                    layerSchema.GeometryType,
                    layerSchema.Srid,
                    _serviceClient.Options.PreferLatestWkid,
                    _serviceClient.Options.FixInvalidGeometries);
            }

            return null;
        }
    }

    private static IReadOnlyDictionary<string, object?> ReadAttributes(JsonElement attributesElement) {
        if (attributesElement.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null) {
            return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        }

        var attributes = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        foreach (var property in attributesElement.EnumerateObject()) {
            attributes[property.Name] = ConvertJsonValue(property.Value);
        }

        return attributes;
    }

    private static object? ConvertJsonValue(JsonElement value) {
        return value.ValueKind switch {
            JsonValueKind.Undefined => null,
            JsonValueKind.Null => null,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number when value.TryGetInt64(out var intValue) => intValue,
            JsonValueKind.Number when value.TryGetDecimal(out var decimalValue) => decimalValue,
            JsonValueKind.Number => value.GetDouble(),
            _ => value.Clone()
        };
    }

    private static long? ConvertToInt64(object? value) {
        return value switch {
            null => null,
            long longValue => longValue,
            int intValue => intValue,
            decimal decimalValue => (long)decimalValue,
            double doubleValue => (long)doubleValue,
            string stringValue when long.TryParse(stringValue, out var parsed) => parsed,
            _ => null
        };
    }

    private static int ResolvePageSize(FeatureQuery query, FeatureLayerSchema schema) {
        var requested = query.PageSize;
        var configured = schema.MaxRecordCount;

        var candidates = new List<int>();

        if (requested is > 0) {
            candidates.Add(requested.Value);
        }

        if (schema.MaxRecordCount is > 0) {
            candidates.Add(schema.MaxRecordCount.Value);
        }

        return candidates.Count == 0
            ? 1000
            : candidates.Min();
    }

    private static IEnumerable<IReadOnlyList<long>> Chunk(IReadOnlyList<long> items, int size) {
        for (var index = 0; index < items.Count; index += size) {
            var count = Math.Min(size, items.Count - index);
            var batch = new long[count];

            for (var i = 0; i < count; i++) {
                batch[i] = items[index + i];
            }

            yield return batch;
        }
    }
}