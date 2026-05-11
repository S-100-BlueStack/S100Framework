using NetTopologySuite.Geometries;
using S100Framework.REST.Models;
using Xunit;

namespace S100Framework.REST.Tests.Clients;

public sealed class CreateReplicaRequestValidationTests
{
    [Fact]
    public void Validate_Throws_WhenLayersAreMissing() {
        var request = new CreateReplicaRequest();

        var exception = Assert.Throws<InvalidOperationException>(() => request.Validate());

        Assert.Contains("At least one layer ID", exception.Message);
    }

    [Fact]
    public void Validate_Throws_WhenLayersContainNegativeValues() {
        var request = CreateValidRequest() with {
            Layers = [-1]
        };

        var exception = Assert.Throws<InvalidOperationException>(() => request.Validate());

        Assert.Contains("negative", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_Throws_WhenLayersContainDuplicates() {
        var request = CreateValidRequest() with {
            Layers = [0, 0]
        };

        var exception = Assert.Throws<InvalidOperationException>(() => request.Validate());

        Assert.Contains("duplicate", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_Throws_WhenReplicaNameIsWhitespace() {
        var request = CreateValidRequest() with {
            ReplicaName = "   "
        };

        var exception = Assert.Throws<InvalidOperationException>(() => request.Validate());

        Assert.Contains("ReplicaName", exception.Message);
    }

    [Fact]
    public void Validate_Throws_WhenSpatialFilterIsMissingForSyncableReplica() {
        var request = CreateValidRequest() with {
            SpatialFilter = null,
            SyncModel = CreateReplicaSyncModel.PerReplica
        };

        var exception = Assert.Throws<InvalidOperationException>(() => request.Validate());

        Assert.Contains("SpatialFilter", exception.Message);
    }

    [Fact]
    public void Validate_AllowsMissingSpatialFilterForSyncModelNone() {
        var request = CreateValidRequest() with {
            SpatialFilter = null,
            SyncModel = CreateReplicaSyncModel.None
        };

        request.Validate();
    }

    [Fact]
    public void Validate_Throws_WhenReturnAttachmentsDataByUrlRequiresReturnAttachments() {
        var request = CreateValidRequest() with {
            ReturnAttachments = false,
            ReturnAttachmentsDataByUrl = true
        };

        var exception = Assert.Throws<InvalidOperationException>(() => request.Validate());

        Assert.Contains("ReturnAttachmentsDataByUrl", exception.Message);
    }

    [Fact]
    public void Validate_Throws_WhenSqliteUsesEmbeddedTransport() {
        var request = CreateValidRequest() with {
            DataFormat = CreateReplicaDataFormat.Sqlite,
            TransportType = CreateReplicaTransportType.Embedded
        };

        var exception = Assert.Throws<InvalidOperationException>(() => request.Validate());

        Assert.Contains("SQLite", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_Throws_WhenAsyncUsesEmbeddedTransport() {
        var request = CreateValidRequest() with {
            IsAsync = true,
            TransportType = CreateReplicaTransportType.Embedded
        };

        var exception = Assert.Throws<InvalidOperationException>(() => request.Validate());

        Assert.Contains("Async", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_Throws_WhenLayerQueryKeyIsNotPresentInLayers() {
        var request = CreateValidRequest() with {
            LayerQueries = new Dictionary<int, CreateReplicaLayerQuery> {
                [7] = new() {
                    Where = "OBJECTID > 0"
                }
            }
        };

        var exception = Assert.Throws<InvalidOperationException>(() => request.Validate());

        Assert.Contains("Layer query key '7'", exception.Message);
    }

    [Fact]
    public void Validate_DoesNotThrow_ForValidRequest() {
        var request = CreateValidRequest();

        request.Validate();
    }

    private static CreateReplicaRequest CreateValidRequest() {
        return new CreateReplicaRequest {
            Layers = [0],
            SpatialFilter = FeatureSpatialFilter.FromEnvelope(
                new Envelope(10, 20, 30, 40),
                inSrid: 4326)
        };
    }
}