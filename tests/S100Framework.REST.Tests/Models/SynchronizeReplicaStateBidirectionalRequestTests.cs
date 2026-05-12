using S100Framework.REST.Models;
using Xunit;

namespace S100Framework.REST.Tests.Models;

public sealed class SynchronizeReplicaStateBidirectionalRequestTests
{
    [Fact]
    public void Validate_DoesNotThrow_WhenStructuredEditsAreProvided() {
        var request = new SynchronizeReplicaStateBidirectionalRequest {
            Edits = CreateEdits()
        };

        request.Validate();
    }

    [Fact]
    public void Validate_DoesNotThrow_WhenRawEditsJsonIsProvided() {
        var request = new SynchronizeReplicaStateBidirectionalRequest {
            EditsJson = """{"layers":[]}"""
        };

        request.Validate();
    }

    [Fact]
    public void Validate_DoesNotThrow_WhenEditsUploadIdIsProvided() {
        var request = new SynchronizeReplicaStateBidirectionalRequest {
            EditsUploadId = "upload-1"
        };

        request.Validate();
    }

    [Fact]
    public void Validate_Throws_WhenNoPayloadIsProvided() {
        var request = new SynchronizeReplicaStateBidirectionalRequest();

        var exception = Assert.Throws<InvalidOperationException>(() => request.Validate());

        Assert.Contains("requires Edits, EditsJson, or EditsUploadId", exception.Message);
    }

    [Fact]
    public void Validate_Throws_WhenMultiplePayloadsAreProvided() {
        var request = new SynchronizeReplicaStateBidirectionalRequest {
            Edits = CreateEdits(),
            EditsUploadId = "upload-1"
        };

        var exception = Assert.Throws<InvalidOperationException>(() => request.Validate());

        Assert.Contains("Only one", exception.Message);
    }

    [Fact]
    public void Validate_Throws_WhenRawEditsJsonIsInvalid() {
        var request = new SynchronizeReplicaStateBidirectionalRequest {
            EditsJson = "{"
        };

        var exception = Assert.Throws<InvalidOperationException>(() => request.Validate());

        Assert.Contains("valid JSON", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_Throws_WhenPollingOptionsAreInvalid() {
        var request = new SynchronizeReplicaStateBidirectionalRequest {
            EditsJson = """{"layers":[]}""",
            PollingOptions = new ReplicaPollingOptions {
                PollInterval = TimeSpan.FromSeconds(1),
                Timeout = TimeSpan.FromSeconds(1)
            }
        };

        var exception = Assert.Throws<InvalidOperationException>(() => request.Validate());

        Assert.Contains("PollInterval", exception.Message);
    }

    [Fact]
    public void Validate_DoesNotRequireThrowOnEditErrorsSpecificConfiguration() {
        var request = new SynchronizeReplicaStateBidirectionalRequest {
            EditsJson = """{"layers":[]}""",
            ThrowOnEditErrors = true
        };

        request.Validate();
    }

    [Fact]
    public void Validate_Throws_WhenEditsUploadFormatIsInvalid() {
        var request = new SynchronizeReplicaStateBidirectionalRequest {
            EditsUploadId = "upload-1",
            EditsUploadFormat = (SynchronizeReplicaEditsUploadFormat)999
        };

        var exception = Assert.Throws<InvalidOperationException>(() => request.Validate());

        Assert.Contains("EditsUploadFormat", exception.Message);
    }

    private static ReplicaEdits CreateEdits() {
        return new ReplicaEdits {
            Layers = [
                new ReplicaLayerEdits {
                    Id = 0,
                    AddsJson = "[]"
                }
            ]
        };
    }
}