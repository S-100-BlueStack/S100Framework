namespace S100Framework.REST.Models;

public sealed record FeatureServiceApplyEditsSubmissionResult(
    FeatureServiceApplyEditsResult? Result,
    Uri? StatusUrl)
{
    public bool IsPending => StatusUrl is not null && Result is null;
}