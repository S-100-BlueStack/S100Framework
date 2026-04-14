using System.Runtime.CompilerServices;
using System.Text.Json;
using NetTopologySuite.Geometries;
using S100Framework.REST.Abstractions;
using S100Framework.REST.Internal.Dto;
using S100Framework.REST.Internal.EsriGeometry;
using S100Framework.REST.Models;

namespace S100Framework.REST.Clients;

public sealed class FeatureLayerClient : IFeatureLayerClient
{
    private readonly FeatureServiceClient _serviceClient;
    private readonly int _layerId;
    private readonly SemaphoreSlim _schemaLock = new(1, 1);

    private FeatureLayerSchema? _schema;

    internal FeatureLayerClient(FeatureServiceClient serviceClient, int layerId) {
        _serviceClient = serviceClient;
        _layerId = layerId;
    }

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

    public async IAsyncEnumerable<FeatureRecord> QueryAsync(
        FeatureQuery query,
        [EnumeratorCancellation] CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(query);

        var schema = await GetSchemaAsync(cancellationToken);
        var pageSize = ResolvePageSize(query, schema);

        if (schema.SupportsPagination) {
            await foreach (var record in QueryWithOffsetPaginationAsync(schema, query, pageSize, cancellationToken)) {
                yield return record;
            }

            yield break;
        }

        await foreach (var record in QueryWithObjectIdFallbackAsync(schema, query, pageSize, cancellationToken)) {
            yield return record;
        }
    }

    public Task<long> QueryCountAsync(
        FeatureQuery query,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(query);
        return _serviceClient.QueryCountAsync(_layerId, query, cancellationToken);
    }

    public async Task<IReadOnlyList<long>> QueryObjectIdsAsync(
        FeatureQuery query,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(query);

        var response = await _serviceClient.QueryIdsAsync(_layerId, query, cancellationToken);
        return response.ObjectIds?.ToArray() ?? Array.Empty<long>();
    }

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
                resultOffset: offset,
                resultRecordCount: requestSize,
                objectIds: null,
                cancellationToken);

            var features = response.Features ?? new List<EsriFeatureDto>();

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
        var objectIds = idsResponse.ObjectIds ?? new List<long>();

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

            var records = (response.Features ?? new List<EsriFeatureDto>())
                .Select(feature => MapFeature(schema, feature))
                .ToList();

            if (string.IsNullOrWhiteSpace(query.OrderBy)) {
                var positionById = batch
                    .Select((id, index) => new { id, index })
                    .ToDictionary(x => x.id, x => x.index);

                foreach (var record in records.OrderBy(record =>
                             record.ObjectId is long id && positionById.TryGetValue(id, out var index)
                                 ? index
                                 : int.MaxValue)) {
                    yield return record;
                }

                continue;
            }

            foreach (var record in records) {
                yield return record;
            }
        }
    }

    private FeatureRecord MapFeature(FeatureLayerSchema schema, EsriFeatureDto feature) {
        var attributes = ReadAttributes(feature.Attributes);

        var geometry = feature.Geometry.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null
            ? null
            : EsriGeometryReader.Read(
                feature.Geometry,
                schema.GeometryType,
                schema.Srid,
                _serviceClient.Options.PreferLatestWkid,
                _serviceClient.Options.FixInvalidGeometries);

        long? objectId = null;

        if (!string.IsNullOrWhiteSpace(schema.ObjectIdFieldName) &&
            attributes.TryGetValue(schema.ObjectIdFieldName, out var rawObjectId)) {
            objectId = ConvertToInt64(rawObjectId);
        }

        return new FeatureRecord(geometry, attributes, objectId);
    }

    private FeatureRecord MapRelatedRecord(
    EsriFeatureDto feature,
    string? geometryType,
    int? defaultSrid,
    string? objectIdFieldName) {
        var attributes = ReadAttributes(feature.Attributes);

        var geometry = feature.Geometry.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null
            ? null
            : EsriGeometryReader.Read(
                feature.Geometry,
                geometryType,
                defaultSrid,
                _serviceClient.Options.PreferLatestWkid,
                _serviceClient.Options.FixInvalidGeometries);

        long? objectId = null;

        if (!string.IsNullOrWhiteSpace(objectIdFieldName) &&
            attributes.TryGetValue(objectIdFieldName, out var rawObjectId)) {
            objectId = ConvertToInt64(rawObjectId);
        }

        return new FeatureRecord(geometry, attributes, objectId);
    }

    private AttachmentInfo MapAttachmentInfo(
    JsonElement attachmentInfoElement,
    long? parentObjectId,
    string? parentGlobalId) {
        var attributes = ReadObjectAttributes(attachmentInfoElement);

        long attachmentId = 0;

        if (TryGetInt64(attributes, "id", out var id)) {
            attachmentId = id;
        }
        else if (TryGetInt64(attributes, "attachmentid", out var attachmentIdAlias)) {
            attachmentId = attachmentIdAlias;
        }

        string? globalId = TryGetString(attributes, "globalId") ?? TryGetString(attributes, "globalid");
        string? name = TryGetString(attributes, "name") ?? TryGetString(attributes, "att_name");
        string? contentType = TryGetString(attributes, "contentType") ?? TryGetString(attributes, "content_type");
        long? size = TryGetNullableInt64(attributes, "size") ?? TryGetNullableInt64(attributes, "data_size");
        string? url = TryGetString(attributes, "url");

        return new AttachmentInfo(
            attachmentId,
            parentObjectId,
            parentGlobalId,
            name,
            contentType,
            size,
            globalId,
            url,
            attributes);
    }

    private static IReadOnlyDictionary<string, object?> ReadObjectAttributes(JsonElement objectElement) {
        if (objectElement.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null) {
            return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        }

        var attributes = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        foreach (var property in objectElement.EnumerateObject()) {
            attributes[property.Name] = ConvertJsonValue(property.Value);
        }

        return attributes;
    }

    private static bool TryGetInt64(
        IReadOnlyDictionary<string, object?> attributes,
        string name,
        out long value) {
        value = default;

        if (!attributes.TryGetValue(name, out var raw)) {
            return false;
        }

        var converted = ConvertToInt64(raw);

        if (!converted.HasValue) {
            return false;
        }

        value = converted.Value;
        return true;
    }

    private static long? TryGetNullableInt64(
        IReadOnlyDictionary<string, object?> attributes,
        string name) {
        return attributes.TryGetValue(name, out var raw)
            ? ConvertToInt64(raw)
            : null;
    }

    private static string? TryGetString(
        IReadOnlyDictionary<string, object?> attributes,
        string name) {
        if (!attributes.TryGetValue(name, out var raw)) {
            return null;
        }

        return raw switch {
            null => null,
            string stringValue => stringValue,
            _ => Convert.ToString(raw, System.Globalization.CultureInfo.InvariantCulture)
        };
    }

    private int? ResolveSrid(EsriSpatialReferenceDto? spatialReference) {
        if (spatialReference is null) {
            return null;
        }

        return _serviceClient.Options.PreferLatestWkid
            ? spatialReference.LatestWkid ?? spatialReference.Wkid
            : spatialReference.Wkid ?? spatialReference.LatestWkid;
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
    public async Task<IReadOnlyList<StatisticRow>> QueryStatisticsAsync(
    FeatureStatisticsQuery query,
    CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(query);

        var response = await _serviceClient.QueryStatisticsAsync(_layerId, query, cancellationToken);

        return (response.Features ?? new List<EsriFeatureDto>())
            .Select(feature => new StatisticRow(ReadAttributes(feature.Attributes)))
            .ToArray();
    }
    public async Task<IReadOnlyList<RelatedRecordGroup>> QueryRelatedRecordsAsync(
    RelatedRecordsQuery query,
    CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(query);

        var response = await _serviceClient.QueryRelatedRecordsAsync(_layerId, query, cancellationToken);

        var objectIdFieldName = response.Fields?
            .FirstOrDefault(field => string.Equals(field.Type, "esriFieldTypeOID", StringComparison.OrdinalIgnoreCase))
            ?.Name;

        var srid = ResolveSrid(response.SpatialReference);

        var groups = (response.RelatedRecordGroups ?? new List<EsriRelatedRecordGroupDto>())
            .Select(group => new RelatedRecordGroup(
                group.ObjectId,
                (group.RelatedRecords ?? new List<EsriFeatureDto>())
                    .Select(feature => MapRelatedRecord(
                        feature,
                        response.GeometryType,
                        srid,
                        objectIdFieldName))
                    .ToArray()))
            .ToArray();

        return groups;
    }

    public async Task<IReadOnlyList<AttachmentGroup>> QueryAttachmentsAsync(
    AttachmentQuery query,
    CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(query);

        var response = await _serviceClient.QueryAttachmentsAsync(_layerId, query, cancellationToken);

        return (response.AttachmentGroups ?? new List<EsriAttachmentGroupDto>())
            .Select(group => new AttachmentGroup(
                group.ParentObjectId,
                group.ParentGlobalId,
                (group.AttachmentInfos ?? new List<JsonElement>())
                    .Select(info => MapAttachmentInfo(
                        info,
                        group.ParentObjectId,
                        group.ParentGlobalId))
                    .ToArray()))
            .ToArray();
    }

    public Task<AttachmentContent> DownloadAttachmentAsync(
        long objectId,
        long attachmentId,
        CancellationToken cancellationToken = default) {
        return _serviceClient.DownloadAttachmentAsync(_layerId, objectId, attachmentId, cancellationToken);
    }
}