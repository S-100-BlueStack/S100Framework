using S100Framework.REST.Models;
using Xunit;

namespace S100Framework.REST.Tests.Models;

public sealed class CreateReplicaJobStatusTests
{
    [Theory]
    [InlineData("Completed")]
    [InlineData("completed_with_errors")]
    [InlineData("Completed With Errors")]
    [InlineData("esriJobSucceeded")]
    public void IsCompleted_NormalizesTerminalSuccessStatuses(string statusValue) {
        var status = CreateStatus(statusValue);

        Assert.True(status.IsCompleted);
        Assert.True(status.IsTerminal);
    }

    [Theory]
    [InlineData("Pending")]
    [InlineData("In Progress")]
    [InlineData("Exporting-Data")]
    [InlineData("ProvisioningReplica")]
    [InlineData("esriJobExecuting")]
    public void IsRunning_NormalizesRunningStatuses(string statusValue) {
        var status = CreateStatus(statusValue);

        Assert.True(status.IsRunning);
        Assert.False(status.IsTerminal);
    }

    [Theory]
    [InlineData("Failed")]
    [InlineData("esriJobFailed")]
    public void IsFailed_NormalizesFailedStatuses(string statusValue) {
        var status = CreateStatus(statusValue);

        Assert.True(status.IsFailed);
        Assert.True(status.IsTerminal);
    }

    private static CreateReplicaJobStatus CreateStatus(string status) {
        return new CreateReplicaJobStatus(
            Status: status,
            ReplicaName: null,
            ReplicaId: null,
            ResponseType: null,
            TransportType: null,
            TargetType: null,
            ResultUrl: null,
            SubmissionTime: null,
            LastUpdatedTime: null);
    }
}