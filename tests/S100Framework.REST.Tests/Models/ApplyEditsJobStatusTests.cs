using S100Framework.REST.Models;
using Xunit;

namespace S100Framework.REST.Tests.Models;

public sealed class ApplyEditsJobStatusTests
{
    [Theory]
    [InlineData("completed")]
    [InlineData("COMPLETED")]
    [InlineData("Completed")]
    [InlineData("completed_with_errors")]
    [InlineData("completed-with-errors")]
    [InlineData("Completed With Errors")]
    [InlineData("Succeeded")]
    [InlineData("Success")]
    [InlineData("esriJobSucceeded")]
    public void IsCompleted_ReturnsTrue_ForCompletedStatusVariants(string statusValue)
    {
        var status = new ApplyEditsJobStatus(
            statusValue,
            ResultUrl: new Uri("https://example.test/result.json"),
            SubmissionTime: null,
            LastUpdatedTime: null);

        Assert.True(status.IsCompleted);
        Assert.True(status.IsTerminal);
    }

    [Theory]
    [InlineData("failed")]
    [InlineData("FAILED")]
    [InlineData("Error")]
    [InlineData("esriJobFailed")]
    [InlineData("cancelled")]
    [InlineData("canceled")]
    [InlineData("esriJobCancelled")]
    [InlineData("esriJobCanceled")]
    [InlineData("timed_out")]
    [InlineData("timed-out")]
    [InlineData("Timed Out")]
    [InlineData("esriJobTimedOut")]
    public void IsTerminal_ReturnsTrue_ForNonCompletedTerminalStatusVariants(string statusValue)
    {
        var status = new ApplyEditsJobStatus(
            statusValue,
            ResultUrl: null,
            SubmissionTime: null,
            LastUpdatedTime: null);

        Assert.False(status.IsCompleted);
        Assert.True(status.IsTerminal);
    }

    [Theory]
    [InlineData("Submitted")]
    [InlineData("Waiting")]
    [InlineData("Executing")]
    [InlineData("Running")]
    [InlineData("Processing")]
    [InlineData("Unknown")]
    [InlineData("")]
    [InlineData("   ")]
    public void IsTerminal_ReturnsFalse_ForNonTerminalStatusVariants(string statusValue)
    {
        var status = new ApplyEditsJobStatus(
            statusValue,
            ResultUrl: null,
            SubmissionTime: null,
            LastUpdatedTime: null);

        Assert.False(status.IsCompleted);
        Assert.False(status.IsTerminal);
    }

    [Theory]
    [InlineData("Submitted")]
    [InlineData("Waiting")]
    [InlineData("Executing")]
    [InlineData("InProgress")]
    [InlineData("in-progress")]
    [InlineData("Processing")]
    [InlineData("Running")]
    [InlineData("Cancelling")]
    [InlineData("esriJobSubmitted")]
    [InlineData("esriJobWaiting")]
    [InlineData("esriJobExecuting")]
    [InlineData("esriJobCancelling")]
    public void IsRunning_ReturnsTrue_ForRunningStatusVariants(string statusValue) {
        var status = new ApplyEditsJobStatus(
            statusValue,
            ResultUrl: null,
            SubmissionTime: null,
            LastUpdatedTime: null);

        Assert.True(status.IsRunning);
        Assert.False(status.IsCompleted);
        Assert.False(status.IsFailed);
        Assert.False(status.IsCancelled);
        Assert.False(status.IsTimedOut);
        Assert.False(status.IsTerminal);
    }

    [Theory]
    [InlineData("failed")]
    [InlineData("FAILED")]
    [InlineData("Error")]
    [InlineData("esriJobFailed")]
    public void IsFailed_ReturnsTrue_ForFailedStatusVariants(string statusValue) {
        var status = new ApplyEditsJobStatus(
            statusValue,
            ResultUrl: null,
            SubmissionTime: null,
            LastUpdatedTime: null);

        Assert.False(status.IsRunning);
        Assert.False(status.IsCompleted);
        Assert.True(status.IsFailed);
        Assert.False(status.IsCancelled);
        Assert.False(status.IsTimedOut);
        Assert.True(status.IsTerminal);
    }

    [Theory]
    [InlineData("cancelled")]
    [InlineData("canceled")]
    [InlineData("esriJobCancelled")]
    [InlineData("esriJobCanceled")]
    public void IsCancelled_ReturnsTrue_ForCancelledStatusVariants(string statusValue) {
        var status = new ApplyEditsJobStatus(
            statusValue,
            ResultUrl: null,
            SubmissionTime: null,
            LastUpdatedTime: null);

        Assert.False(status.IsRunning);
        Assert.False(status.IsCompleted);
        Assert.False(status.IsFailed);
        Assert.True(status.IsCancelled);
        Assert.False(status.IsTimedOut);
        Assert.True(status.IsTerminal);
    }

    [Theory]
    [InlineData("timed_out")]
    [InlineData("timed-out")]
    [InlineData("Timed Out")]
    [InlineData("Timeout")]
    [InlineData("esriJobTimedOut")]
    public void IsTimedOut_ReturnsTrue_ForTimedOutStatusVariants(string statusValue) {
        var status = new ApplyEditsJobStatus(
            statusValue,
            ResultUrl: null,
            SubmissionTime: null,
            LastUpdatedTime: null);

        Assert.False(status.IsRunning);
        Assert.False(status.IsCompleted);
        Assert.False(status.IsFailed);
        Assert.False(status.IsCancelled);
        Assert.True(status.IsTimedOut);
        Assert.True(status.IsTerminal);
    }
}