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

        if (layerId < 0) {
            throw new ArgumentOutOfRangeException(nameof(layerId), layerId, "Layer ID must not be negative.");
        }

        request.Validate();

        var endpointUri = UriUtility.AppendPath(
            _serviceUri,
            $"{layerId.ToString(CultureInfo.InvariantCulture)}/calculate");

        var dto = await PostFormAsync<EsriCalculateResponseDto>(
            endpointUri,
            CreateCalculateParameters(request),
            cancellationToken);

        if (!dto.Success.HasValue) {
            throw new FeatureServiceException(
                "The server returned a calculate response without a success value.",
                endpointUri);
        }

        return new CalculateResult(
            dto.Success.Value,
            dto.UpdatedFeatureCount,
            dto.EditMoment);
    }

    private static Dictionary<string, string?> CreateCalculateParameters(CalculateRequest request) {
        var parameters = new Dictionary<string, string?> {
            ["f"] = "json",
            ["where"] = string.IsNullOrWhiteSpace(request.Where) ? "1=1" : request.Where,
            ["calcExpression"] = SerializeCalculateExpressions(request.Expressions),
            ["gdbVersion"] = request.GdbVersion,
            ["sessionID"] = request.SessionId,
            ["returnEditMoment"] = request.ReturnEditMoment ? "true" : null
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
}