namespace S100Framework.REST.Models;

public sealed record ExtractChangesSubmissionResult(
    ExtractChangesResult? Result,
    Uri? StatusUrl)
{
    public bool IsPending => StatusUrl is not null && Result is null;
}