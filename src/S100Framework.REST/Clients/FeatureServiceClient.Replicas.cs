using System.Globalization;
using System.Text.Json;
using S100Framework.REST.Exceptions;
using S100Framework.REST.Internal.Dto;
using S100Framework.REST.Internal.Http;
using S100Framework.REST.Models;

namespace S100Framework.REST.Clients;

/// <summary>
/// Provides service-level <c>replicas</c> resource operations for feature service endpoints.
/// </summary>
public sealed partial class FeatureServiceClient
{
    /// <inheritdoc />
    public async Task<FeatureServiceReplicasResult> GetReplicasAsync(
        FeatureServiceReplicasRequest? request = null,
        CancellationToken cancellationToken = default) {
        request ??= new FeatureServiceReplicasRequest();
        request.Validate();

        var metadata = await GetMetadataAsync(cancellationToken);

        if (!metadata.Capabilities.SupportsSync && !metadata.Capabilities.SyncEnabled) {
            throw new FeatureServiceCapabilityException(
                "The feature service does not support sync, so the replicas resource is not available.");
        }

        var uri = UriUtility.WithQuery(
            UriUtility.AppendPath(_serviceUri, "replicas"),
            new Dictionary<string, string?> {
                ["f"] = "json",
                ["replicaVersion"] = request.ReplicaVersion,
                ["returnVersion"] = request.ReturnVersion ? "true" : null,
                ["returnLastSyncDate"] = request.ReturnLastSyncDate ? "true" : null
            });

        var dto = await GetAsync<List<EsriFeatureServiceReplicaDto?>>(uri, cancellationToken);

        return new FeatureServiceReplicasResult(
            dto
                .Where(static replica => replica is not null)
                .Select(replica => MapFeatureServiceReplica(replica!, uri))
                .ToArray());
    }

    private static FeatureServiceReplica MapFeatureServiceReplica(
        EsriFeatureServiceReplicaDto dto,
        Uri requestUri) {
        if (string.IsNullOrWhiteSpace(dto.ReplicaName)) {
            throw new FeatureServiceException(
                "The replicas resource returned a replica without a replicaName.",
                requestUri);
        }

        if (string.IsNullOrWhiteSpace(dto.ReplicaId)) {
            throw new FeatureServiceException(
                "The replicas resource returned a replica without a replicaID.",
                requestUri);
        }

        return new FeatureServiceReplica(
            dto.ReplicaName,
            dto.ReplicaId,
            string.IsNullOrWhiteSpace(dto.ReplicaVersion) ? null : dto.ReplicaVersion,
            ReadOptionalReplicaTimestamp(dto.LastSyncDate, requestUri, "lastSyncDate"));
    }

    private static long? ReadOptionalReplicaTimestamp(
        JsonElement? element,
        Uri requestUri,
        string propertyName) {
        if (!element.HasValue ||
            element.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) {
            return null;
        }

        if (!TryReadReplicaTimestamp(element.Value, out var value)) {
            throw new FeatureServiceException(
                $"The replicas resource returned an invalid {propertyName} value.",
                requestUri);
        }

        if (value < 0) {
            throw new FeatureServiceException(
                $"The replicas resource returned a negative {propertyName} value.",
                requestUri);
        }

        return value;
    }

    private static bool TryReadReplicaTimestamp(
        JsonElement element,
        out long value) {
        value = 0;

        return element.ValueKind switch {
            JsonValueKind.Number => element.TryGetInt64(out value),
            JsonValueKind.String => long.TryParse(
                element.GetString(),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out value),
            _ => false
        };
    }
}