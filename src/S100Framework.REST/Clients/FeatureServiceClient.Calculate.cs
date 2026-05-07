using System.Globalization;
using System.Text.Json;
using S100Framework.REST.Exceptions;
using S100Framework.REST.Internal.Dto;
using S100Framework.REST.Internal.Http;
using S100Framework.REST.Models;

namespace S100Framework.REST.Clients;

/// <summary>
/// Provides layer-level <c>calculate</c> operations for feature service endpoints.
/// </summary>
public sealed partial class FeatureServiceClient
{
    internal async Task<CalculateResult> CalculateAsync(
        int layerId,
        CalculateRequest request,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(request);

        ValidateCalculateLayerId(layerId);

        request.Validate();

        var endpointUri = CreateCalculateEndpointUri(layerId);

        var dto = await PostFormAsync<EsriCalculateResponseDto>(
            endpointUri,
            CreateCalculateParameters(request, async: false),
            cancellationToken);

        return MapCalculateResult(dto, endpointUri);
    }

    internal async Task<CalculateSubmissionResult> SubmitCalculateAsync(
        int layerId,
        CalculateRequest request,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(request);

        ValidateCalculateLayerId(layerId);

        request.Validate();

        var endpointUri = CreateCalculateEndpointUri(layerId);

        using var document = await PostFormAsync<JsonDocument>(
            endpointUri,
            CreateCalculateParameters(request, async: true),
            cancellationToken);

        var root = document.RootElement;

        var statusUrl = ReadOptionalAbsoluteUri(
     root,
     endpointUri,
     "calculate",
     "statusUrl",
     "statusURL");

        if (statusUrl is not null) {
            return new CalculateSubmissionResult(
                Result: null,
                StatusUrl: statusUrl);
        }

        var dto = root.Deserialize<EsriCalculateResponseDto>(JsonOptions)
                  ?? throw new FeatureServiceException(
                      "The calculate submission payload could not be deserialized.",
                      endpointUri);

        return new CalculateSubmissionResult(
            Result: MapCalculateResult(dto, endpointUri),
            StatusUrl: null);
    }

    internal async Task<CalculateJobStatus> GetCalculateStatusAsync(
        Uri statusUrl,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(statusUrl);

        using var document = await GetAsync<JsonDocument>(statusUrl, cancellationToken);
        var root = document.RootElement;

        return new CalculateJobStatus(
            Status: TryGetString(root, "status", "jobStatus", out var status)
                ? status ?? "Unknown"
                : "Unknown",
            RecordCount: TryGetInt64(root, "recordCount", out var recordCount)
                ? recordCount
                : null,
            SubmissionTime: TryGetInt64(root, "submissionTime", out var submissionTime)
                ? submissionTime
                : null,
            LastUpdatedTime: TryGetInt64(root, "lastUpdatedTime", out var lastUpdatedTime)
                ? lastUpdatedTime
                : null);
    }

    private static void ValidateCalculateLayerId(int layerId) {
        if (layerId < 0) {
            throw new ArgumentOutOfRangeException(nameof(layerId), layerId, "Layer ID must not be negative.");
        }
    }

    private Uri CreateCalculateEndpointUri(int layerId) {
        return UriUtility.AppendPath(
            _serviceUri,
            $"{layerId.ToString(CultureInfo.InvariantCulture)}/calculate");
    }

    private static Dictionary<string, string?> CreateCalculateParameters(
        CalculateRequest request,
        bool async) {
        var parameters = new Dictionary<string, string?> {
            ["f"] = "json",
            ["where"] = string.IsNullOrWhiteSpace(request.Where) ? "1=1" : request.Where,
            ["calcExpression"] = SerializeCalculateExpressions(request.Expressions),
            ["gdbVersion"] = request.GdbVersion,
            ["sessionID"] = request.SessionId,
            ["returnEditMoment"] = request.ReturnEditMoment ? "true" : null,
            ["async"] = async ? "true" : "false"
        };

        if (request.SqlFormat.HasValue && request.SqlFormat.Value != FeatureQuerySqlFormat.None) {
            parameters["sqlFormat"] = request.SqlFormat.Value switch {
                FeatureQuerySqlFormat.Standard => "standard",
                FeatureQuerySqlFormat.Native => "native",
                _ => throw new ArgumentOutOfRangeException(
                    nameof(request.SqlFormat),
                    request.SqlFormat,
                    "Unsupported calculate SQL format.")
            };
        }

        return parameters;
    }

    private static string SerializeCalculateExpressions(IReadOnlyList<CalculateExpression> expressions) {
        var payload = expressions
            .Select(static expression => expression.ToJsonPayload())
            .ToArray();

        return JsonSerializer.Serialize(payload, JsonOptions);
    }

    private static CalculateResult MapCalculateResult(
        EsriCalculateResponseDto dto,
        Uri requestUri) {
        if (!dto.Success.HasValue) {
            throw new FeatureServiceException(
                "The server returned a calculate response without a success value.",
                requestUri);
        }

        return new CalculateResult(
            dto.Success.Value,
            dto.UpdatedFeatureCount,
            dto.EditMoment);
    }
}