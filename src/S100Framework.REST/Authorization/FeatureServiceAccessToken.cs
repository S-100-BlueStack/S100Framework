namespace S100Framework.REST.Authorization;

/// <summary>
/// Represents an access token together with its UTC expiration timestamp.
/// </summary>
/// <param name="Token">
/// The raw access token value.
/// </param>
/// <param name="ExpiresAtUtc">
/// The UTC timestamp when the token expires.
/// </param>
public sealed record FeatureServiceAccessToken(
    string Token,
    DateTimeOffset ExpiresAtUtc)
{
    /// <summary>
    /// Determines whether the token is expired at the specified point in time.
    /// </summary>
    /// <param name="utcNow">
    /// The current UTC timestamp.
    /// </param>
    /// <returns>
    /// <see langword="true" /> when the token is expired; otherwise <see langword="false" />.
    /// </returns>
    public bool IsExpired(DateTimeOffset utcNow) {
        return utcNow >= ExpiresAtUtc;
    }

    /// <summary>
    /// Determines whether the token should be refreshed before it expires.
    /// </summary>
    /// <param name="utcNow">
    /// The current UTC timestamp.
    /// </param>
    /// <param name="refreshBeforeExpiration">
    /// The refresh window to apply before the expiration timestamp.
    /// </param>
    /// <returns>
    /// <see langword="true" /> when the token is inside the configured refresh window;
    /// otherwise <see langword="false" />.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <paramref name="refreshBeforeExpiration" /> is negative.
    /// </exception>
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