using System.Globalization;
using S100Framework.REST.Exceptions;
using S100Framework.REST.Internal.Dto;
using S100Framework.REST.Internal.Http;
using S100Framework.REST.Models;

namespace S100Framework.REST.Clients;

/// <summary>
/// Provides direct layer-level <c>deleteFeatures</c> operations for feature service endpoints.
/// </summary>
public sealed partial class FeatureServiceClient
{
    internal async Task<DeleteFeaturesResult> DeleteFeaturesAsync(
        int layerId,
        DeleteFeaturesRequest request,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(request);

        if (layerId < 0) {
            throw new ArgumentOutOfRangeException(nameof(layerId), layerId, "Layer ID must not be negative.");
        }

        request.Validate();

        var endpointUri = UriUtility.AppendPath(
            _serviceUri,
            $"{layerId.ToString(CultureInfo.InvariantCulture)}/deleteFeatures");

        var dto = await PostFormAsync<EsriDeleteFeaturesResponseDto>(
            endpointUri,
            CreateDeleteFeaturesParameters(request),
            cancellationToken);

        return MapDeleteFeaturesResult(dto, endpointUri);
    }

    private static Dictionary<string, string?> CreateDeleteFeaturesParameters(
        DeleteFeaturesRequest request) {
        var parameters = new Dictionary<string, string?> {
            ["f"] = "json",
            ["objectIds"] = request.ObjectIds is { Count: > 0 }
                ? string.Join(",", request.ObjectIds)
                : null,
            ["where"] = request.Where,
            ["gdbVersion"] = request.GdbVersion,
            ["rollbackOnFailure"] = request.RollbackOnFailure ? "true" : "false",
            ["returnDeleteResults"] = request.ReturnDeleteResults ? "true" : "false",
            ["returnEditMoment"] = request.ReturnEditMoment ? "true" : null
        };

        ApplySpatialFilter(parameters, request.SpatialFilter);

        return parameters;
    }

    private static DeleteFeaturesResult MapDeleteFeaturesResult(
        EsriDeleteFeaturesResponseDto dto,
        Uri requestUri) {
        var deleteResults = MapEditResults(dto.DeleteResults);

        if (!dto.Success.HasValue && deleteResults.Count == 0) {
            throw new FeatureServiceException(
                "The server returned a deleteFeatures response without a success value or delete results.",
                requestUri);
        }

        var success = dto.Success ?? deleteResults.All(static result => result.Success);

        return new DeleteFeaturesResult(
            success,
            deleteResults,
            dto.EditMoment);
    }
}