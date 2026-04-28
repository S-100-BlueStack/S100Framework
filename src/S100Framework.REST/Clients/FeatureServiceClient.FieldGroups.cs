using System.Globalization;
using S100Framework.REST.Internal.Dto;
using S100Framework.REST.Internal.Http;
using S100Framework.REST.Models;

namespace S100Framework.REST.Clients;

/// <summary>
/// Provides field group operations for feature service layer endpoints.
/// </summary>
public sealed partial class FeatureServiceClient
{
    internal async Task<FeatureLayerFieldGroupsResult> GetFieldGroupsAsync(
        int layerId,
        CancellationToken cancellationToken = default) {
        if (layerId < 0) {
            throw new ArgumentOutOfRangeException(nameof(layerId), layerId, "Layer ID must not be negative.");
        }

        var uri = UriUtility.WithQuery(
            UriUtility.AppendPath(
                UriUtility.AppendPath(_serviceUri, layerId.ToString(CultureInfo.InvariantCulture)),
                "fieldGroups"),
            new Dictionary<string, string?> {
                ["f"] = "json"
            });

        var dto = await GetAsync<EsriFieldGroupsResponseDto>(uri, cancellationToken);

        return new FeatureLayerFieldGroupsResult(
            layerId,
            (dto.FieldGroups ?? new List<EsriFieldGroupDto>())
                .Select(static fieldGroup => new FeatureFieldGroup(
                    fieldGroup.Name ?? string.Empty,
                    fieldGroup.Restrictive,
                    fieldGroup.Fields?
                        .Where(static field => !string.IsNullOrWhiteSpace(field))
                        .ToArray() ?? Array.Empty<string>()))
                .ToArray());
    }
}