namespace S100Framework.REST.Authorization;

public sealed record FeatureServiceAccessToken(
    string Token,
    DateTimeOffset ExpiresAtUtc)
{
    public bool IsExpired(DateTimeOffset utcNow) {
        return utcNow >= ExpiresAtUtc;
    }

    public bool ShouldRefresh(
        DateTimeOffset utcNow,
        TimeSpan refreshBeforeExpiration) {
        if (refreshBeforeExpiration < TimeSpan.Zero) {
            throw new InvalidOperationException(
                "RefreshBeforeExpiration must be greater than or equal to zero.");
        }

        return utcNow >= ExpiresAtUtc - refreshBeforeExpiration;
    }
}