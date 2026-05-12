using S100Framework.REST.Extensions;
using S100Framework.REST.Models;
using Xunit;

namespace S100Framework.REST.Tests.Extensions;

public sealed class FeatureServiceMetadataReplicaExtensionsTests
{
    [Fact]
    public void SupportsReplicaResource_ReturnsTrue_WhenSyncAndSyncCapabilitiesAreAdvertised() {
        var metadata = CreateMetadata(
            supportsSync: true,
            syncEnabled: false,
            syncCapabilities: CreateSyncCapabilities());

        Assert.True(metadata.SupportsReplicaResource());
    }

    [Fact]
    public void SupportsReplicaResource_ReturnsTrue_WhenSyncEnabledIsAdvertisedWithoutCapabilityString() {
        var metadata = CreateMetadata(
            supportsSync: false,
            syncEnabled: true,
            syncCapabilities: CreateSyncCapabilities());

        Assert.True(metadata.SupportsReplicaResource());
    }

    [Fact]
    public void SupportsReplicaResource_ReturnsFalse_WhenSyncCapabilitiesAreMissing() {
        var metadata = CreateMetadata(
            supportsSync: true,
            syncEnabled: false,
            syncCapabilities: null);

        Assert.False(metadata.SupportsReplicaResource());
    }

    [Fact]
    public void SupportsAsyncReplicaJobs_ReturnsTrue_WhenAsyncIsAdvertised() {
        var metadata = CreateMetadata(
            supportsSync: true,
            syncEnabled: false,
            syncCapabilities: CreateSyncCapabilities() with {
                SupportsAsync = true
            });

        Assert.True(metadata.SupportsAsyncReplicaJobs());
    }

    [Fact]
    public void SupportsAsyncReplicaJobs_ReturnsFalse_WhenCoreReplicaResourceIsUnavailable() {
        var metadata = CreateMetadata(
            supportsSync: true,
            syncEnabled: false,
            syncCapabilities: null);

        Assert.False(metadata.SupportsAsyncReplicaJobs());
    }

    [Theory]
    [InlineData(CreateReplicaSyncModel.PerReplica, true)]
    [InlineData(CreateReplicaSyncModel.PerLayer, true)]
    [InlineData(CreateReplicaSyncModel.None, true)]
    public void SupportsCreateReplica_ReturnsTrue_ForSupportedSyncModels(
        CreateReplicaSyncModel syncModel,
        bool expected) {
        var metadata = CreateMetadata(
            supportsSync: true,
            syncEnabled: false,
            syncCapabilities: CreateSyncCapabilities());

        Assert.Equal(expected, metadata.SupportsCreateReplica(syncModel));
    }

    [Fact]
    public void SupportsCreateReplica_ReturnsFalse_WhenRequestedCreateSyncModelIsUnsupported() {
        var metadata = CreateMetadata(
            supportsSync: true,
            syncEnabled: false,
            syncCapabilities: CreateSyncCapabilities() with {
                SupportsPerLayerSync = false
            });

        Assert.False(metadata.SupportsCreateReplica(CreateReplicaSyncModel.PerLayer));
    }

    [Theory]
    [InlineData(SynchronizeReplicaSyncModel.PerReplica, true)]
    [InlineData(SynchronizeReplicaSyncModel.PerLayer, true)]
    public void SupportsSynchronizeReplica_ReturnsTrue_ForSupportedSyncModels(
        SynchronizeReplicaSyncModel syncModel,
        bool expected) {
        var metadata = CreateMetadata(
            supportsSync: true,
            syncEnabled: false,
            syncCapabilities: CreateSyncCapabilities());

        Assert.Equal(expected, metadata.SupportsSynchronizeReplica(syncModel));
    }

    [Fact]
    public void SupportsSynchronizeReplica_ReturnsFalse_WhenRequestedSyncModelIsUnsupported() {
        var metadata = CreateMetadata(
            supportsSync: true,
            syncEnabled: false,
            syncCapabilities: CreateSyncCapabilities() with {
                SupportsPerReplicaSync = false
            });

        Assert.False(metadata.SupportsSynchronizeReplica(SynchronizeReplicaSyncModel.PerReplica));
    }

    [Fact]
    public void GetReplicaCapabilityIssues_ReturnsEmptyList_WhenCoreReplicaResourceIsAvailable() {
        var metadata = CreateMetadata(
            supportsSync: true,
            syncEnabled: false,
            syncCapabilities: CreateSyncCapabilities());

        Assert.Empty(metadata.GetReplicaCapabilityIssues());
    }

    [Fact]
    public void GetReplicaCapabilityIssues_ReturnsIssues_WhenSyncAndSyncCapabilitiesAreMissing() {
        var metadata = CreateMetadata(
            supportsSync: false,
            syncEnabled: false,
            syncCapabilities: null);

        var issues = metadata.GetReplicaCapabilityIssues();

        Assert.Contains("The feature service does not advertise Sync capability.", issues);
        Assert.Contains("The feature service does not expose syncCapabilities metadata.", issues);
    }

    [Fact]
    public void GetReplicaCapabilityIssues_ReturnsIssue_WhenNoSyncModelsAreAdvertised() {
        var metadata = CreateMetadata(
            supportsSync: true,
            syncEnabled: false,
            syncCapabilities: CreateSyncCapabilities() with {
                SupportsPerReplicaSync = false,
                SupportsPerLayerSync = false,
                SupportsSyncModelNone = false
            });

        var issue = Assert.Single(metadata.GetReplicaCapabilityIssues());

        Assert.Equal(
            "The feature service does not advertise any supported replica sync models.",
            issue);
    }

    [Fact]
    public void SupportsCreateReplica_Throws_WhenCreateReplicaSyncModelIsInvalid() {
        var metadata = CreateMetadata(
            supportsSync: true,
            syncEnabled: false,
            syncCapabilities: CreateSyncCapabilities());

        var exception = Assert.Throws<InvalidOperationException>(() =>
            metadata.SupportsCreateReplica((CreateReplicaSyncModel)999));

        Assert.Contains("SyncModel", exception.Message);
    }

    [Fact]
    public void SupportsSynchronizeReplica_Throws_WhenSynchronizeReplicaSyncModelIsInvalid() {
        var metadata = CreateMetadata(
            supportsSync: true,
            syncEnabled: false,
            syncCapabilities: CreateSyncCapabilities());

        var exception = Assert.Throws<InvalidOperationException>(() =>
            metadata.SupportsSynchronizeReplica((SynchronizeReplicaSyncModel)999));

        Assert.Contains("SyncModel", exception.Message);
    }

    private static FeatureServiceMetadata CreateMetadata(
     bool supportsSync,
     bool syncEnabled,
     FeatureServiceSyncCapabilities? syncCapabilities) {
        return new FeatureServiceMetadata(
            new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer"),
            [
                new FeatureServiceDatasetInfo(0, "Layer 0")
            ],
            Array.Empty<FeatureServiceDatasetInfo>(),
            supportsSync ? "Query,Sync" : "Query",
            null,
            new FeatureServiceCapabilities(
                SupportsQuery: true,
                SupportsCreate: false,
                SupportsUpdate: false,
                SupportsDelete: false,
                SupportsEditing: false,
                SupportsUploads: false,
                SupportsSync: supportsSync,
                SupportsChangeTracking: false,
                SyncEnabled: syncEnabled,
                SupportsAsyncApplyEdits: false),
            extractChangesCapabilities: null,
            supportedAppendFormats: Array.Empty<string>(),
            supportedExportFormats: Array.Empty<string>(),
            syncCapabilities: syncCapabilities);
    }

    private static FeatureServiceSyncCapabilities CreateSyncCapabilities() {
        return new FeatureServiceSyncCapabilities(
            SupportsRegisteringExistingData: false,
            SupportsSyncDirectionControl: true,
            SupportsPerLayerSync: true,
            SupportsPerReplicaSync: true,
            SupportsSyncModelNone: true,
            SupportsRollbackOnFailure: false,
            SupportsAsync: true,
            SupportsAttachmentsSyncDirection: false,
            SupportsBiDirectionalSyncForServer: false);
    }
}