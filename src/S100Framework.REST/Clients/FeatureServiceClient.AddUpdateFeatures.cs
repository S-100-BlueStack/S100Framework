using System.Globalization;
using S100Framework.REST.Exceptions;
using S100Framework.REST.Internal.Dto;
using S100Framework.REST.Internal.EsriGeometry;
using S100Framework.REST.Internal.Http;
using S100Framework.REST.Models;

namespace S100Framework.REST.Clients;

/// <summary>
/// Provides direct layer-level <c>addFeatures</c> and <c>updateFeatures</c> operations for feature service endpoints.
/// </summary>
public sealed partial class FeatureServiceClient
{
    internal async Task<AddFeaturesResult> AddFeaturesAsync(
        int layerId,
        AddFeaturesRequest request,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(request);

        ValidateFeatureEditLayerId(layerId);

        request.Validate();

        var endpointUri = UriUtility.AppendPath(
            _serviceUri,
            $"{layerId.ToString(CultureInfo.InvariantCulture)}/addFeatures");

        var dto = await PostFormAsync<EsriAddFeaturesResponseDto>(
            endpointUri,
            CreateAddFeaturesParameters(request),
            cancellationToken);

        return MapAddFeaturesResult(dto, endpointUri);
    }

    internal async Task<UpdateFeaturesResult> UpdateFeaturesAsync(
        int layerId,
        UpdateFeaturesRequest request,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(request);

        ValidateFeatureEditLayerId(layerId);

        request.Validate();

        var endpointUri = UriUtility.AppendPath(
            _serviceUri,
            $"{layerId.ToString(CultureInfo.InvariantCulture)}/updateFeatures");

        var dto = await PostFormAsync<EsriUpdateFeaturesResponseDto>(
            endpointUri,
            CreateUpdateFeaturesParameters(request),
            cancellationToken);

        return MapUpdateFeaturesResult(dto, endpointUri);
    }

    private static void ValidateFeatureEditLayerId(int layerId) {
        if (layerId < 0) {
            throw new ArgumentOutOfRangeException(nameof(layerId), layerId, "Layer ID must not be negative.");
        }
    }

    private static Dictionary<string, string?> CreateAddFeaturesParameters(
        AddFeaturesRequest request) {
        return new Dictionary<string, string?> {
            ["f"] = "json",
            ["features"] = EsriEditGeometryWriter.WriteFeatures(request.Features),
            ["gdbVersion"] = request.GdbVersion,
            ["rollbackOnFailure"] = request.RollbackOnFailure ? "true" : "false",
            ["useGlobalIds"] = request.UseGlobalIds ? "true" : "false",
            ["returnEditMoment"] = request.ReturnEditMoment ? "true" : null
        };
    }

    private static Dictionary<string, string?> CreateUpdateFeaturesParameters(
        UpdateFeaturesRequest request) {
        return new Dictionary<string, string?> {
            ["f"] = "json",
            ["features"] = EsriEditGeometryWriter.WriteFeatures(request.Features),
            ["gdbVersion"] = request.GdbVersion,
            ["rollbackOnFailure"] = request.RollbackOnFailure ? "true" : "false",
            ["useGlobalIds"] = request.UseGlobalIds ? "true" : "false",
            ["returnEditMoment"] = request.ReturnEditMoment ? "true" : null
        };
    }

    private static AddFeaturesResult MapAddFeaturesResult(
        EsriAddFeaturesResponseDto dto,
        Uri requestUri) {
        var addResults = dto.AddResults?
            .Select(MapEditResult)
            .ToArray() ?? Array.Empty<EditResult>();

        if (addResults.Length == 0) {
            throw new FeatureServiceException(
                "The server returned an addFeatures response without add results.",
                requestUri);
        }

        return new AddFeaturesResult(
            addResults.All(static result => result.Success),
            addResults,
            dto.EditMoment);
    }

    private static UpdateFeaturesResult MapUpdateFeaturesResult(
        EsriUpdateFeaturesResponseDto dto,
        Uri requestUri) {
        var updateResults = dto.UpdateResults?
            .Select(MapEditResult)
            .ToArray() ?? Array.Empty<EditResult>();

        if (updateResults.Length == 0) {
            throw new FeatureServiceException(
                "The server returned an updateFeatures response without update results.",
                requestUri);
        }

        return new UpdateFeaturesResult(
            updateResults.All(static result => result.Success),
            updateResults,
            dto.EditMoment);
    }
}