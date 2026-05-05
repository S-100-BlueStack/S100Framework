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
}