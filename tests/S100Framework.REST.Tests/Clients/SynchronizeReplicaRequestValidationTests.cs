using S100Framework.REST.Models;
using Xunit;

namespace S100Framework.REST.Tests.Clients;

public sealed class SynchronizeReplicaRequestValidationTests
{
    [Fact]
    public void Validate_Throws_WhenReplicaIdIsMissing() {
        var request = new SynchronizeReplicaRequest {
            ReplicaId = " ",
            ReplicaServerGen = 1
        };

        var exception = Assert.Throws<InvalidOperationException>(() => request.Validate());

        Assert.Contains("ReplicaId", exception.Message);
    }

    [Fact]
    public void Validate_Throws_WhenPerReplicaDownloadHasNoReplicaServerGen() {
        var request = new SynchronizeReplicaRequest {
            ReplicaId = "replica-1",
            SyncModel = SynchronizeReplicaSyncModel.PerReplica,
            SyncDirection = SynchronizeReplicaSyncDirection.Download
        };

        var exception = Assert.Throws<InvalidOperationException>(() => request.Validate());

        Assert.Contains("ReplicaServerGen", exception.Message);
    }

    [Fact]
    public void Validate_AllowsPerReplicaSnapshotWithoutReplicaServerGen() {
        var request = new SynchronizeReplicaRequest {
            ReplicaId = "replica-1",
            SyncModel = SynchronizeReplicaSyncModel.PerReplica,
            SyncDirection = SynchronizeReplicaSyncDirection.Snapshot
        };

        request.Validate();
    }

    [Fact]
    public void Validate_Throws_WhenReplicaServerGenIsNegative() {
        var request = CreateValidPerReplicaRequest() with {
            ReplicaServerGen = -1
        };

        var exception = Assert.Throws<InvalidOperationException>(() => request.Validate());

        Assert.Contains("ReplicaServerGen", exception.Message);
    }

    [Fact]
    public void Validate_Throws_WhenPerLayerHasNoSyncLayers() {
        var request = new SynchronizeReplicaRequest {
            ReplicaId = "replica-1",
            SyncModel = SynchronizeReplicaSyncModel.PerLayer
        };

        var exception = Assert.Throws<InvalidOperationException>(() => request.Validate());

        Assert.Contains("SyncLayers", exception.Message);
    }

    [Fact]
    public void Validate_Throws_WhenPerLayerAlsoHasReplicaServerGen() {
        var request = new SynchronizeReplicaRequest {
            ReplicaId = "replica-1",
            SyncModel = SynchronizeReplicaSyncModel.PerLayer,
            ReplicaServerGen = 1,
            SyncLayers = [
                new SynchronizeReplicaSyncLayer {
                    Id = 0,
                    ServerGen = 1
                }
            ]
        };

        var exception = Assert.Throws<InvalidOperationException>(() => request.Validate());

        Assert.Contains("ReplicaServerGen", exception.Message);
    }

    [Fact]
    public void Validate_Throws_WhenPerReplicaAlsoHasSyncLayers() {
        var request = CreateValidPerReplicaRequest() with {
            SyncLayers = [
                new SynchronizeReplicaSyncLayer {
                    Id = 0,
                    ServerGen = 1
                }
            ]
        };

        var exception = Assert.Throws<InvalidOperationException>(() => request.Validate());

        Assert.Contains("SyncLayers", exception.Message);
    }

    [Fact]
    public void Validate_Throws_WhenSyncLayerHasDuplicateIds() {
        var request = new SynchronizeReplicaRequest {
            ReplicaId = "replica-1",
            SyncModel = SynchronizeReplicaSyncModel.PerLayer,
            SyncLayers = [
                new SynchronizeReplicaSyncLayer {
                    Id = 0,
                    ServerGen = 1
                },
                new SynchronizeReplicaSyncLayer {
                    Id = 0,
                    ServerGen = 2
                }
            ]
        };

        var exception = Assert.Throws<InvalidOperationException>(() => request.Validate());

        Assert.Contains("duplicate", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_Throws_WhenDownloadSyncLayerHasNoServerGen() {
        var request = new SynchronizeReplicaRequest {
            ReplicaId = "replica-1",
            SyncModel = SynchronizeReplicaSyncModel.PerLayer,
            SyncLayers = [
                new SynchronizeReplicaSyncLayer {
                    Id = 0,
                    SyncDirection = SynchronizeReplicaSyncDirection.Download
                }
            ]
        };

        var exception = Assert.Throws<InvalidOperationException>(() => request.Validate());

        Assert.Contains("ServerGen", exception.Message);
    }

    [Fact]
    public void Validate_AllowsSnapshotSyncLayerWithoutServerGen() {
        var request = new SynchronizeReplicaRequest {
            ReplicaId = "replica-1",
            SyncModel = SynchronizeReplicaSyncModel.PerLayer,
            SyncLayers = [
                new SynchronizeReplicaSyncLayer {
                    Id = 0,
                    SyncDirection = SynchronizeReplicaSyncDirection.Snapshot
                }
            ]
        };

        request.Validate();
    }

    [Fact]
    public void Validate_Throws_WhenSqliteUsesEmbeddedTransport() {
        var request = CreateValidPerReplicaRequest() with {
            DataFormat = SynchronizeReplicaDataFormat.Sqlite,
            TransportType = SynchronizeReplicaTransportType.Embedded
        };

        var exception = Assert.Throws<InvalidOperationException>(() => request.Validate());

        Assert.Contains("SQLite", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_Throws_WhenAsyncUsesEmbeddedTransport() {
        var request = CreateValidPerReplicaRequest() with {
            IsAsync = true,
            TransportType = SynchronizeReplicaTransportType.Embedded
        };

        var exception = Assert.Throws<InvalidOperationException>(() => request.Validate());

        Assert.Contains("Async", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_DoesNotThrow_ForValidPerReplicaRequest() {
        var request = CreateValidPerReplicaRequest();

        request.Validate();
    }

    private static SynchronizeReplicaRequest CreateValidPerReplicaRequest() {
        return new SynchronizeReplicaRequest {
            ReplicaId = "replica-1",
            ReplicaServerGen = 1
        };
    }

    [Fact]
    public void Validate_Throws_WhenUploadHasNoEditsPayload() {
        var request = new SynchronizeReplicaRequest {
            ReplicaId = "replica-1",
            SyncDirection = SynchronizeReplicaSyncDirection.Upload
        };

        var exception = Assert.Throws<InvalidOperationException>(() => request.Validate());

        Assert.Contains("EditsJson", exception.Message);
        Assert.Contains("EditsUploadId", exception.Message);
    }

    [Fact]
    public void Validate_Throws_WhenBidirectionalHasNoEditsPayload() {
        var request = new SynchronizeReplicaRequest {
            ReplicaId = "replica-1",
            ReplicaServerGen = 1,
            SyncDirection = SynchronizeReplicaSyncDirection.Bidirectional
        };

        var exception = Assert.Throws<InvalidOperationException>(() => request.Validate());

        Assert.Contains("EditsJson", exception.Message);
        Assert.Contains("EditsUploadId", exception.Message);
    }

    [Fact]
    public void Validate_Throws_WhenEditsJsonAndEditsUploadIdAreBothProvided() {
        var request = new SynchronizeReplicaRequest {
            ReplicaId = "replica-1",
            SyncDirection = SynchronizeReplicaSyncDirection.Upload,
            EditsJson = """{"layers":[]}""",
            EditsUploadId = "upload-1"
        };

        var exception = Assert.Throws<InvalidOperationException>(() => request.Validate());

        Assert.Contains("cannot both be provided", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_Throws_WhenEditsJsonIsInvalid() {
        var request = new SynchronizeReplicaRequest {
            ReplicaId = "replica-1",
            SyncDirection = SynchronizeReplicaSyncDirection.Upload,
            EditsJson = "{"
        };

        var exception = Assert.Throws<InvalidOperationException>(() => request.Validate());

        Assert.Contains("valid JSON", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_Throws_WhenEditsJsonIsScalar() {
        var request = new SynchronizeReplicaRequest {
            ReplicaId = "replica-1",
            SyncDirection = SynchronizeReplicaSyncDirection.Upload,
            EditsJson = "\"not an object\""
        };

        var exception = Assert.Throws<InvalidOperationException>(() => request.Validate());

        Assert.Contains("JSON object or array", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_Throws_WhenRollbackOnFailureIsUsedWithDownload() {
        var request = CreateValidPerReplicaRequest() with {
            RollbackOnFailure = true
        };

        var exception = Assert.Throws<InvalidOperationException>(() => request.Validate());

        Assert.Contains("RollbackOnFailure", exception.Message);
    }

    [Fact]
    public void Validate_Throws_WhenReturnIdsForAddsIsUsedWithDownload() {
        var request = CreateValidPerReplicaRequest() with {
            ReturnIdsForAdds = true
        };

        var exception = Assert.Throws<InvalidOperationException>(() => request.Validate());

        Assert.Contains("ReturnIdsForAdds", exception.Message);
    }

    [Fact]
    public void Validate_Throws_WhenBidirectionalPerReplicaHasNoReplicaServerGen() {
        var request = new SynchronizeReplicaRequest {
            ReplicaId = "replica-1",
            SyncDirection = SynchronizeReplicaSyncDirection.Bidirectional,
            EditsJson = """{"layers":[]}"""
        };

        var exception = Assert.Throws<InvalidOperationException>(() => request.Validate());

        Assert.Contains("ReplicaServerGen", exception.Message);
    }

    [Fact]
    public void Validate_AllowsUploadWithoutReplicaServerGen() {
        var request = new SynchronizeReplicaRequest {
            ReplicaId = "replica-1",
            SyncDirection = SynchronizeReplicaSyncDirection.Upload,
            EditsJson = """{"layers":[]}"""
        };

        request.Validate();
    }

    [Fact]
    public void Validate_Throws_WhenBidirectionalSyncLayerHasNoServerGen() {
        var request = new SynchronizeReplicaRequest {
            ReplicaId = "replica-1",
            SyncModel = SynchronizeReplicaSyncModel.PerLayer,
            SyncDirection = SynchronizeReplicaSyncDirection.Bidirectional,
            EditsJson = """{"layers":[]}""",
            SyncLayers = [
                new SynchronizeReplicaSyncLayer {
                Id = 0,
                SyncDirection = SynchronizeReplicaSyncDirection.Bidirectional
            }
            ]
        };

        var exception = Assert.Throws<InvalidOperationException>(() => request.Validate());

        Assert.Contains("ServerGen", exception.Message);
    }

    [Fact]
    public void Validate_AllowsUploadSyncLayerWithoutServerGen() {
        var request = new SynchronizeReplicaRequest {
            ReplicaId = "replica-1",
            SyncModel = SynchronizeReplicaSyncModel.PerLayer,
            SyncDirection = SynchronizeReplicaSyncDirection.Upload,
            EditsJson = """{"layers":[]}""",
            SyncLayers = [
                new SynchronizeReplicaSyncLayer {
                Id = 0,
                SyncDirection = SynchronizeReplicaSyncDirection.Upload
            }
            ]
        };

        request.Validate();
    }

    [Fact]
    public void Validate_Throws_WhenEditsUploadFormatIsInvalid() {
        var request = new SynchronizeReplicaRequest {
            ReplicaId = "replica-1",
            SyncDirection = SynchronizeReplicaSyncDirection.Upload,
            EditsUploadId = "upload-1",
            EditsUploadFormat = (SynchronizeReplicaEditsUploadFormat)999
        };

        var exception = Assert.Throws<InvalidOperationException>(() => request.Validate());

        Assert.Contains("EditsUploadFormat", exception.Message);
    }
}