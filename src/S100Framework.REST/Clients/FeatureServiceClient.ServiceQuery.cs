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

        var metadata = await GetMetadataAsync(cancellationToken);

        if (!metadata.Capabilities.SupportsQuery) {
            throw new FeatureServiceCapabilityException(
                "The feature service does not advertise query support.");
        }

        var uri = UriUtility.WithQuery(
            UriUtility.AppendPath(_serviceUri, "query"),
            CreateServiceQueryParameters(request));

        var dto = await GetAsync<EsriServiceQueryResponseDto>(
            uri,
            cancellationToken);

        return new FeatureServiceQueryResult(
            (dto.Layers ?? new List<EsriServiceQueryLayerDto>())
                .Select(MapServiceQueryLayer)
                .ToArray());
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
        EsriServiceQueryLayerDto dto) {
        var srid = ResolveServiceQueryLayerSrid(dto.SpatialReference);

        return new FeatureServiceLayerQueryResult(
            dto.Id,
            (dto.Features ?? new List<EsriFeatureDto>())
                .Select(feature => MapServiceQueryFeature(
                    dto.GeometryType,
                    srid,
                    dto.ObjectIdFieldName,
                    feature))
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