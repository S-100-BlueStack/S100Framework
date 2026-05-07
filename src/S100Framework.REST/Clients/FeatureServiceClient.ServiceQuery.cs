using System.Globalization;
using System.Text.Json;
using S100Framework.REST.Exceptions;
using S100Framework.REST.Internal.Dto;
using S100Framework.REST.Internal.EsriGeometry;
using S100Framework.REST.Internal.Http;
using S100Framework.REST.Internal.Json;
using S100Framework.REST.Models;

namespace S100Framework.REST.Clients;

/// <summary>
/// Provides service-level <c>query</c> operations for feature service endpoints.
/// </summary>
public sealed partial class FeatureServiceClient
{
    /// <inheritdoc />
    public async Task<FeatureServiceQueryResult> QueryAsync(
        FeatureServiceQueryRequest request,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(request);

        request.Validate();

        await EnsureServiceQuerySupportAsync(cancellationToken);

        var parameters = CreateServiceQueryParameters(request);

        var dto = await SendServiceQueryAsync<EsriServiceQueryResponseDto>(
            parameters,
            cancellationToken);

        var endpointUri = UriUtility.AppendPath(_serviceUri, "query");

        return new FeatureServiceQueryResult(
            EnumerateServiceQueryLayers(dto.Layers)
                .Select(layer => MapServiceQueryLayer(layer, endpointUri))
                .ToArray());
    }

    /// <inheritdoc />
    public async Task<FeatureServiceQueryResult> QueryAllAsync(
        FeatureServiceQueryRequest request,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(request);

        request.Validate();

        await EnsureServiceQuerySupportAsync(cancellationToken);

        var layers = new List<FeatureServiceLayerQueryResult>(request.LayerDefinitions.Count);

        foreach (var layerDefinition in request.LayerDefinitions) {
            var records = new List<FeatureRecord>();

            await foreach (var record in GetLayerClient(layerDefinition.LayerId)
                .QueryAsync(CreateLayerFeatureQuery(request, layerDefinition), cancellationToken)) {
                records.Add(record);
            }

            layers.Add(new FeatureServiceLayerQueryResult(
                layerDefinition.LayerId,
                records.ToArray(),
                ExceededTransferLimit: null));
        }

        return new FeatureServiceQueryResult(layers);
    }

    /// <inheritdoc />
    public async Task<FeatureServiceQueryCountResult> QueryCountAsync(
        FeatureServiceQueryRequest request,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(request);

        request.Validate();

        await EnsureServiceQuerySupportAsync(cancellationToken);

        var parameters = CreateServiceQueryParameters(request);
        parameters["returnCountOnly"] = "true";

        var dto = await SendServiceQueryAsync<EsriServiceQueryResponseDto>(
            parameters,
            cancellationToken);

        var endpointUri = UriUtility.AppendPath(_serviceUri, "query");

        return new FeatureServiceQueryCountResult(
            EnumerateServiceQueryLayers(dto.Layers)
                .Select(layer => new FeatureServiceLayerCountResult(
                    ReadRequiredServiceQueryLayerId(layer.Id, endpointUri),
                    layer.Count ?? 0))
                .ToArray());
    }

    /// <inheritdoc />
    public async Task<FeatureServiceQueryObjectIdsResult> QueryObjectIdsAsync(
        FeatureServiceQueryRequest request,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(request);

        request.Validate();

        await EnsureServiceQuerySupportAsync(cancellationToken);

        var parameters = CreateServiceQueryParameters(request);
        parameters["returnIdsOnly"] = "true";

        var dto = await SendServiceQueryAsync<EsriServiceQueryResponseDto>(
            parameters,
            cancellationToken);

        var endpointUri = UriUtility.AppendPath(_serviceUri, "query");

        return new FeatureServiceQueryObjectIdsResult(
            EnumerateServiceQueryLayers(dto.Layers)
                .Select(layer => new FeatureServiceLayerObjectIdsResult(
                    ReadRequiredServiceQueryLayerId(layer.Id, endpointUri),
                    layer.ObjectIdFieldName,
                    ReadServiceQueryObjectIds(layer.ObjectIds)))
                .ToArray());
    }

    /// <inheritdoc />
    public async Task<FeatureServiceQueryUniqueIdsResult> QueryUniqueIdsAsync(
        FeatureServiceQueryRequest request,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(request);

        request.Validate();

        await EnsureServiceQuerySupportAsync(cancellationToken);

        var parameters = CreateServiceQueryParameters(request);
        parameters["returnUniqueIdsOnly"] = "true";

        var dto = await SendServiceQueryAsync<EsriServiceQueryResponseDto>(
            parameters,
            cancellationToken);

        var endpointUri = UriUtility.AppendPath(_serviceUri, "query");

        return new FeatureServiceQueryUniqueIdsResult(
            EnumerateServiceQueryLayers(dto.Layers)
                .Select(layer => new FeatureServiceLayerUniqueIdsResult(
                    ReadRequiredServiceQueryLayerId(layer.Id, endpointUri),
                    ReadServiceQueryUniqueIdFieldNames(layer.UniqueIdFieldNames),
                    ReadServiceQueryUniqueIds(layer.UniqueIds),
                    layer.ExceededTransferLimit))
                .ToArray());
    }

    /// <inheritdoc />
    public async Task<FeatureServiceQueryExtentsResult> QueryExtentsAsync(
        FeatureServiceQueryRequest request,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(request);

        request.Validate();

        await EnsureServiceQuerySupportAsync(cancellationToken);

        var layers = new List<FeatureServiceLayerExtentResult>(request.LayerDefinitions.Count);

        foreach (var layerDefinition in request.LayerDefinitions) {
            var extent = await QueryExtentAsync(
                layerDefinition.LayerId,
                CreateLayerExtentQuery(request, layerDefinition),
                cancellationToken);

            layers.Add(new FeatureServiceLayerExtentResult(
                layerDefinition.LayerId,
                extent));
        }

        return new FeatureServiceQueryExtentsResult(layers);
    }

    private static FeatureQuery CreateLayerFeatureQuery(
        FeatureServiceQueryRequest request,
        FeatureServiceLayerQueryDefinition layerDefinition) {
        return new FeatureQuery {
            Where = string.IsNullOrWhiteSpace(layerDefinition.Where)
                ? "1=1"
                : layerDefinition.Where,
            OutFields = layerDefinition.OutFields,
            ReturnGeometry = request.ReturnGeometry,
            ReturnZ = request.ReturnZ,
            ReturnM = request.ReturnM,
            OutSrid = request.OutSrid,
            GdbVersion = request.GdbVersion,
            GeometryPrecision = request.GeometryPrecision,
            MaxAllowableOffset = request.MaxAllowableOffset,
            SqlFormat = request.SqlFormat,
            SpatialFilter = request.SpatialFilter,
            TimeInstant = request.TimeInstant,
            TimeExtent = request.TimeExtent,
            HistoricMoment = request.HistoricMoment,
            TimeReferenceUnknownClient = request.TimeReferenceUnknownClient
        };
    }

    private static FeatureQuery CreateLayerExtentQuery(
        FeatureServiceQueryRequest request,
        FeatureServiceLayerQueryDefinition layerDefinition) {
        return new FeatureQuery {
            Where = string.IsNullOrWhiteSpace(layerDefinition.Where)
                ? "1=1"
                : layerDefinition.Where,
            OutSrid = request.OutSrid,
            GdbVersion = request.GdbVersion,
            SqlFormat = request.SqlFormat,
            SpatialFilter = request.SpatialFilter,
            TimeInstant = request.TimeInstant,
            TimeExtent = request.TimeExtent,
            HistoricMoment = request.HistoricMoment,
            TimeReferenceUnknownClient = request.TimeReferenceUnknownClient
        };
    }

    private async Task EnsureServiceQuerySupportAsync(
        CancellationToken cancellationToken) {
        var metadata = await GetMetadataAsync(cancellationToken);

        if (!metadata.Capabilities.SupportsQuery) {
            throw new FeatureServiceCapabilityException(
                "The feature service does not advertise query support.");
        }
    }

    private Task<TResponse> SendServiceQueryAsync<TResponse>(
        Dictionary<string, string?> parameters,
        CancellationToken cancellationToken) {
        var uri = UriUtility.WithQuery(
            UriUtility.AppendPath(_serviceUri, "query"),
            parameters);

        return GetAsync<TResponse>(
            uri,
            cancellationToken);
    }

    private Dictionary<string, string?> CreateServiceQueryParameters(
        FeatureServiceQueryRequest request) {
        var parameters = new Dictionary<string, string?> {
            ["f"] = "json",
            ["layerDefs"] = SerializeServiceQueryLayerDefinitions(request.LayerDefinitions),
            ["returnGeometry"] = request.ReturnGeometry ? "true" : "false",
            ["returnTrueCurves"] = _options.ReturnTrueCurves ? "true" : "false"
        };

        if (request.TimeInstant.HasValue) {
            parameters["time"] = FormatEpochMilliseconds(request.TimeInstant.Value);
        }
        else if (request.TimeExtent is not null) {
            parameters["time"] =
                $"{FormatTimeExtentBound(request.TimeExtent.Start)},{FormatTimeExtentBound(request.TimeExtent.End)}";
        }

        if (request.OutSrid.HasValue) {
            parameters["outSR"] = request.OutSrid.Value.ToString(CultureInfo.InvariantCulture);
        }

        if (!string.IsNullOrWhiteSpace(request.GdbVersion)) {
            parameters["gdbVersion"] = request.GdbVersion;
        }

        if (request.HistoricMoment.HasValue) {
            parameters["historicMoment"] = FormatEpochMilliseconds(request.HistoricMoment.Value);
        }

        if (request.ReturnZ.HasValue) {
            parameters["returnZ"] = request.ReturnZ.Value ? "true" : "false";
        }

        if (request.ReturnM.HasValue) {
            parameters["returnM"] = request.ReturnM.Value ? "true" : "false";
        }

        if (request.GeometryPrecision.HasValue) {
            parameters["geometryPrecision"] = request.GeometryPrecision.Value.ToString(CultureInfo.InvariantCulture);
        }

        if (request.MaxAllowableOffset.HasValue) {
            parameters["maxAllowableOffset"] =
                request.MaxAllowableOffset.Value.ToString(CultureInfo.InvariantCulture);
        }

        if (request.SqlFormat.HasValue && request.SqlFormat.Value != FeatureQuerySqlFormat.None) {
            parameters["sqlFormat"] = MapServiceQuerySqlFormat(request.SqlFormat.Value);
        }

        if (request.TimeReferenceUnknownClient) {
            parameters["timeReferenceUnknownClient"] = "true";
        }

        ApplySpatialFilter(parameters, request.SpatialFilter);

        return parameters;
    }

    private static string SerializeServiceQueryLayerDefinitions(
        IReadOnlyList<FeatureServiceLayerQueryDefinition> layerDefinitions) {
        var payload = layerDefinitions
            .Select(static layerDefinition => new Dictionary<string, object?> {
                ["layerId"] = layerDefinition.LayerId,
                ["where"] = string.IsNullOrWhiteSpace(layerDefinition.Where)
                    ? "1=1"
                    : layerDefinition.Where,
                ["outFields"] = layerDefinition.OutFields is { Count: > 0 }
                    ? string.Join(",", layerDefinition.OutFields)
                    : "*"
            })
            .ToArray();

        return JsonSerializer.Serialize(payload, JsonOptions);
    }

    private FeatureServiceLayerQueryResult MapServiceQueryLayer(
        EsriServiceQueryLayerDto dto,
        Uri endpointUri) {
        var layerId = ReadRequiredServiceQueryLayerId(dto.Id, endpointUri);
        var srid = ResolveServiceQueryLayerSrid(dto.SpatialReference);

        return new FeatureServiceLayerQueryResult(
            layerId,
            (dto.Features ?? Enumerable.Empty<EsriFeatureDto?>())
                .Where(static feature => feature is not null)
                .Select(feature => MapServiceQueryFeature(
                    dto.GeometryType,
                    srid,
                    dto.ObjectIdFieldName,
                    feature!))
                .ToArray(),
            dto.ExceededTransferLimit);
    }

    private FeatureRecord MapServiceQueryFeature(
        string? geometryType,
        int? defaultSrid,
        string? objectIdFieldName,
        EsriFeatureDto feature) {
        var attributes = JsonAttributeValueReader.ReadAttributes(
            feature.Attributes,
            JsonAttributeNumberHandling.DecimalFallback);

        var geometry = feature.Geometry.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null
            ? null
            : EsriGeometryReader.Read(
                feature.Geometry,
                geometryType,
                defaultSrid,
                _options.PreferLatestWkid,
                _options.FixInvalidGeometries,
                _options.TrueCurveHandling,
                _options.CircularArcSegmentCount);

        long? objectId = null;

        if (!string.IsNullOrWhiteSpace(objectIdFieldName) &&
            attributes.TryGetValue(objectIdFieldName, out var rawObjectId)) {
            objectId = ConvertToNullableInt64(rawObjectId);
        }

        return new FeatureRecord(
            geometry,
            attributes,
            objectId);
    }

    private int? ResolveServiceQueryLayerSrid(
        EsriSpatialReferenceDto? spatialReference) {
        if (spatialReference is null) {
            return null;
        }

        return _options.PreferLatestWkid
            ? spatialReference.LatestWkid ?? spatialReference.Wkid
            : spatialReference.Wkid ?? spatialReference.LatestWkid;
    }

    private static IEnumerable<EsriServiceQueryLayerDto> EnumerateServiceQueryLayers(
        IEnumerable<EsriServiceQueryLayerDto?>? layers) {
        return layers?
            .Where(static layer => layer is not null)
            .Select(static layer => layer!) ?? Enumerable.Empty<EsriServiceQueryLayerDto>();
    }

    private static int ReadRequiredServiceQueryLayerId(
        int? layerId,
        Uri endpointUri) {
        if (!layerId.HasValue) {
            throw new FeatureServiceException(
                "The service query payload returned a layer without an ID.",
                endpointUri);
        }

        if (layerId.Value < 0) {
            throw new FeatureServiceException(
                "The service query payload returned a layer with a negative ID.",
                endpointUri);
        }

        return layerId.Value;
    }

    private static IReadOnlyList<long> ReadServiceQueryObjectIds(
        IEnumerable<long?>? objectIds) {
        return objectIds?
            .Where(static objectId => objectId.HasValue)
            .Select(static objectId => objectId!.Value)
            .ToArray() ?? Array.Empty<long>();
    }

    private static IReadOnlyList<string> ReadServiceQueryUniqueIdFieldNames(
        JsonElement element) {
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

    private static IReadOnlyList<FeatureUniqueId> ReadServiceQueryUniqueIds(
        JsonElement element) {
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
                        .Select(ReadServiceQueryUniqueIdComponent)
                        .ToArray()));

                continue;
            }

            result.Add(new FeatureUniqueId([ReadServiceQueryUniqueIdComponent(item)]));
        }

        return result;
    }

    private static string ReadServiceQueryUniqueIdComponent(
        JsonElement element) {
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

    private static string MapServiceQuerySqlFormat(
        FeatureQuerySqlFormat value) {
        return value switch {
            FeatureQuerySqlFormat.None => "none",
            FeatureQuerySqlFormat.Standard => "standard",
            FeatureQuerySqlFormat.Native => "native",
            _ => throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                "Unsupported service query SQL format.")
        };
    }

    private static string FormatEpochMilliseconds(
        DateTimeOffset value) {
        return value.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture);
    }

    private static string FormatTimeExtentBound(
        DateTimeOffset? value) {
        return value.HasValue
            ? FormatEpochMilliseconds(value.Value)
            : "null";
    }
}