using S100Framework.REST.Models;

namespace S100Framework.REST.Extensions;

/// <summary>
/// Provides convenience helpers for evaluating feature service sync and replica capabilities.
/// </summary>
public static class FeatureServiceMetadataReplicaExtensions
{
    /// <summary>
    /// Gets a value indicating whether the service advertises sync support and exposes sync capabilities.
    /// </summary>
    /// <param name="metadata">
    /// The feature service metadata.
    /// </param>
    /// <returns>
    /// <see langword="true" /> when the service can expose replica-related resources; otherwise <see langword="false" />.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="metadata" /> is <see langword="null" />.
    /// </exception>
    public static bool SupportsReplicaResource(this FeatureServiceMetadata metadata) {
        ArgumentNullException.ThrowIfNull(metadata);

        return IsSyncAdvertised(metadata) &&
               metadata.SyncCapabilities is not null;
    }

    /// <summary>
    /// Gets a value indicating whether the service supports asynchronous replica jobs.
    /// </summary>
    /// <param name="metadata">
    /// The feature service metadata.
    /// </param>
    /// <returns>
    /// <see langword="true" /> when async replica jobs are advertised; otherwise <see langword="false" />.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="metadata" /> is <see langword="null" />.
    /// </exception>
    public static bool SupportsAsyncReplicaJobs(this FeatureServiceMetadata metadata) {
        ArgumentNullException.ThrowIfNull(metadata);

        return metadata.SupportsReplicaResource() &&
               metadata.SyncCapabilities!.SupportsAsync;
    }

    /// <summary>
    /// Gets a value indicating whether the service supports a <c>createReplica</c> request with the specified sync model.
    /// </summary>
    /// <param name="metadata">
    /// The feature service metadata.
    /// </param>
    /// <param name="syncModel">
    /// The requested create replica sync model.
    /// </param>
    /// <returns>
    /// <see langword="true" /> when the requested sync model is advertised; otherwise <see langword="false" />.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="metadata" /> is <see langword="null" />.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <paramref name="syncModel" /> is not a supported enum value.
    /// </exception>
    public static bool SupportsCreateReplica(
        this FeatureServiceMetadata metadata,
        CreateReplicaSyncModel syncModel) {
        ArgumentNullException.ThrowIfNull(metadata);

        if (!Enum.IsDefined(syncModel)) {
            throw new InvalidOperationException("SyncModel must be a supported createReplica sync model.");
        }

        if (!metadata.SupportsReplicaResource()) {
            return false;
        }

        return syncModel switch {
            CreateReplicaSyncModel.PerReplica => metadata.SyncCapabilities!.SupportsPerReplicaSync,
            CreateReplicaSyncModel.PerLayer => metadata.SyncCapabilities!.SupportsPerLayerSync,
            CreateReplicaSyncModel.None => metadata.SyncCapabilities!.SupportsSyncModelNone,
            _ => false
        };
    }

    /// <summary>
    /// Gets a value indicating whether the service supports a <c>synchronizeReplica</c> request with the specified sync model.
    /// </summary>
    /// <param name="metadata">
    /// The feature service metadata.
    /// </param>
    /// <param name="syncModel">
    /// The requested synchronize replica sync model.
    /// </param>
    /// <returns>
    /// <see langword="true" /> when the requested sync model is advertised; otherwise <see langword="false" />.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="metadata" /> is <see langword="null" />.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <paramref name="syncModel" /> is not a supported enum value.
    /// </exception>
    public static bool SupportsSynchronizeReplica(
        this FeatureServiceMetadata metadata,
        SynchronizeReplicaSyncModel syncModel) {
        ArgumentNullException.ThrowIfNull(metadata);

        if (!Enum.IsDefined(syncModel)) {
            throw new InvalidOperationException("SyncModel must be a supported synchronizeReplica sync model.");
        }

        if (!metadata.SupportsReplicaResource()) {
            return false;
        }

        return syncModel switch {
            SynchronizeReplicaSyncModel.PerReplica => metadata.SyncCapabilities!.SupportsPerReplicaSync,
            SynchronizeReplicaSyncModel.PerLayer => metadata.SyncCapabilities!.SupportsPerLayerSync,
            _ => false
        };
    }

    /// <summary>
    /// Gets user-readable capability issues that explain why replica operations may not be available.
    /// </summary>
    /// <param name="metadata">
    /// The feature service metadata.
    /// </param>
    /// <returns>
    /// Capability issues. An empty collection means the core replica resource is available.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="metadata" /> is <see langword="null" />.
    /// </exception>
    public static IReadOnlyList<string> GetReplicaCapabilityIssues(this FeatureServiceMetadata metadata) {
        ArgumentNullException.ThrowIfNull(metadata);

        var issues = new List<string>();

        if (!IsSyncAdvertised(metadata)) {
            issues.Add("The feature service does not advertise Sync capability.");
        }

        if (metadata.SyncCapabilities is null) {
            issues.Add("The feature service does not expose syncCapabilities metadata.");
        }

        if (metadata.SyncCapabilities is not null &&
            !metadata.SyncCapabilities.SupportsPerReplicaSync &&
            !metadata.SyncCapabilities.SupportsPerLayerSync &&
            !metadata.SyncCapabilities.SupportsSyncModelNone) {
            issues.Add("The feature service does not advertise any supported replica sync models.");
        }

        return issues;
    }

    private static bool IsSyncAdvertised(FeatureServiceMetadata metadata) {
        return metadata.Capabilities.SupportsSync ||
               metadata.Capabilities.SyncEnabled;
    }
}