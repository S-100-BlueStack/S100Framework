using S100Framework.REST.Exceptions;
using S100Framework.REST.Internal.Dto;
using S100Framework.REST.Internal.Http;
using S100Framework.REST.Models;

namespace S100Framework.REST.Clients;

/// <summary>
/// Provides service-level <c>unRegisterReplica</c> operations for feature service endpoints.
/// </summary>
public sealed partial class FeatureServiceClient
{
    /// <inheritdoc />
    public async Task<UnregisterReplicaResult> UnregisterReplicaAsync(
        UnregisterReplicaRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        request.Validate();

        var metadata = await GetMetadataAsync(cancellationToken);

        if (!metadata.Capabilities.SupportsSync && !metadata.Capabilities.SyncEnabled)
        {
            throw new FeatureServiceCapabilityException(
                "The feature service does not support sync, so unRegisterReplica is not available.");
        }

        var endpointUri = UriUtility.AppendPath(_serviceUri, "unRegisterReplica");

        var dto = await PostFormAsync<EsriUnregisterReplicaResponseDto>(
            endpointUri,
            new Dictionary<string, string?>
            {
                ["f"] = "json",
                ["replicaID"] = request.ReplicaId
            },
            cancellationToken);

        if (!dto.Success.HasValue)
        {
            throw new FeatureServiceException(
                "The server returned an unRegisterReplica response without a success value.",
                endpointUri);
        }

        return new UnregisterReplicaResult(
            request.ReplicaId,
            dto.Success.Value);
    }
}