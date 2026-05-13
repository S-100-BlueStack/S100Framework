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
    /// Gets a value indicating whether the service advertises sync direction control for upload and bidirectional workflows.
    /// </summary>
    /// <param name="metadata">
    /// The feature service metadata.
    /// </param>
    /// <returns>
    /// <see langword="true" /> when sync direction control is advertised; otherwise <see langword="false" />.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="metadata" /> is <see langword="null" />.
    /// </exception>
    public static bool SupportsReplicaSyncDirectionControl(this FeatureServiceMetadata metadata) {
        ArgumentNullException.ThrowIfNull(metadata);

        return metadata.SupportsReplicaResource() &&
               metadata.SyncCapabilities!.SupportsSyncDirectionControl;
    }

    /// <summary>
    /// Gets a value indicating whether the service advertises rollback-on-failure support for upload workflows.
    /// </summary>
    /// <param name="metadata">
    /// The feature service metadata.
    /// </param>
    /// <returns>
    /// <see langword="true" /> when rollback-on-failure is advertised; otherwise <see langword="false" />.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="metadata" /> is <see langword="null" />.
    /// </exception>
    public static bool SupportsReplicaRollbackOnFailure(this FeatureServiceMetadata metadata) {
        ArgumentNullException.ThrowIfNull(metadata);

        return metadata.SupportsReplicaResource() &&
               metadata.SyncCapabilities!.SupportsRollbackOnFailure;
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
    /// Gets user-readable capability issues for core replica support and upload-oriented replica workflows.
    /// </summary>
    /// <param name="metadata">
    /// The feature service metadata.
    /// </param>
    /// <returns>
    /// Capability issues for the currently validated replica workflows. An empty collection means the service advertises
    /// the core replica resource requirements and the upload-oriented capabilities checked by this helper.
    /// </returns>
    /// <remarks>
    /// Use <see cref="SupportsReplicaResource(FeatureServiceMetadata)" /> when only the base replica resource availability
    /// needs to be checked.
    /// </remarks>
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

        if (metadata.SyncCapabilities is not null &&
            !metadata.SyncCapabilities.SupportsSyncDirectionControl) {
            issues.Add("The feature service does not advertise sync direction control for upload or bidirectional replica workflows.");
        }

        if (metadata.SyncCapabilities is not null &&
            !metadata.SyncCapabilities.SupportsRollbackOnFailure) {
            issues.Add("The feature service does not advertise rollback-on-failure support for upload replica workflows.");
        }

        return issues;
    }

    private static bool IsSyncAdvertised(FeatureServiceMetadata metadata) {
        return metadata.Capabilities.SupportsSync ||
               metadata.Capabilities.SyncEnabled;
    }
}