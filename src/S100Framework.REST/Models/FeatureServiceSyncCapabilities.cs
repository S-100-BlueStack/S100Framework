namespace S100Framework.REST.Models;

/// <summary>
/// Describes sync and replica capabilities advertised by a feature service.
/// </summary>
/// <param name="SupportsRegisteringExistingData">
/// Indicates whether existing data can be registered when working with replicas.
/// </param>
/// <param name="SupportsSyncDirectionControl">
/// Indicates whether the service supports selecting a synchronization direction.
/// </param>
/// <param name="SupportsPerLayerSync">
/// Indicates whether the service supports the per-layer sync model.
/// </param>
/// <param name="SupportsPerReplicaSync">
/// Indicates whether the service supports the per-replica sync model.
/// </param>
/// <param name="SupportsSyncModelNone">
/// Indicates whether the service supports creating replicas without sync tracking.
/// </param>
/// <param name="SupportsRollbackOnFailure">
/// Indicates whether sync operations can request rollback-on-failure behavior.
/// </param>
/// <param name="SupportsAsync">
/// Indicates whether async replica/sync operations are advertised.
/// </param>
/// <param name="SupportsAttachmentsSyncDirection">
/// Indicates whether attachment synchronization direction can be controlled.
/// </param>
/// <param name="SupportsBiDirectionalSyncForServer">
/// Indicates whether server-to-server bidirectional sync is advertised.
/// </param>
public sealed record FeatureServiceSyncCapabilities(
    bool SupportsRegisteringExistingData,
    bool SupportsSyncDirectionControl,
    bool SupportsPerLayerSync,
    bool SupportsPerReplicaSync,
    bool SupportsSyncModelNone,
    bool SupportsRollbackOnFailure,
    bool SupportsAsync,
    bool SupportsAttachmentsSyncDirection,
    bool SupportsBiDirectionalSyncForServer)
{
    /// <summary>
    /// Gets the raw <c>supportedSyncDataOptions</c> value advertised by the service, when available.
    /// </summary>
    /// <remarks>
    /// The value is intentionally exposed as the raw ArcGIS bitmask so future option meanings can be supported
    /// without changing the public model shape.
    /// </remarks>
    public int? SupportedSyncDataOptions { get; init; }

    /// <summary>
    /// Gets a value indicating whether sync operations advertise datum transformation support for queries.
    /// </summary>
    /// <remarks>
    /// ArcGIS documentation and real services have used both <c>supportsQueryWithDatumTransformation</c>
    /// and the misspelled <c>supportsQueryWithDatumTransformatiom</c>; both are mapped to this property.
    /// </remarks>
    public bool SupportsQueryWithDatumTransformation { get; init; }
}