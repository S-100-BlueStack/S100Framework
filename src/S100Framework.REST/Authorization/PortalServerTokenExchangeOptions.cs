namespace S100Framework.REST.Authorization;

/// <summary>
/// Configures portal-to-server token exchange for federated ArcGIS services.
/// </summary>
public sealed class PortalServerTokenExchangeOptions
{
    /// <summary>
    /// Gets or sets the portal <c>sharing/rest/generateToken</c> endpoint URI.
    /// </summary>
    public Uri? GenerateTokenUri { get; set; }

    /// <summary>
    /// Gets or sets the federated ArcGIS Server base URL used during token exchange.
    /// </summary>
    public Uri? ServerUrl { get; set; }

    /// <summary>
    /// Gets or sets the timeout for the token exchange request.
    /// </summary>
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets how long before expiration the exchanged server token should be refreshed.
    /// </summary>
    public TimeSpan RefreshBeforeExpiration { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Validates the configured options.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when one or more required option values are missing or invalid.
    /// </exception>
    public void Validate() {
        if (GenerateTokenUri is null) {
            throw new InvalidOperationException("GenerateTokenUri must be configured.");
        }

        if (!GenerateTokenUri.IsAbsoluteUri) {
            throw new InvalidOperationException("GenerateTokenUri must be an absolute URI.");
        }

        if (!string.Equals(GenerateTokenUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)) {
            throw new InvalidOperationException("GenerateTokenUri must use HTTPS.");
        }

        if (!GenerateTokenUri.AbsolutePath.EndsWith(
            "/sharing/rest/generateToken",
            StringComparison.OrdinalIgnoreCase)) {
            throw new InvalidOperationException(
                "GenerateTokenUri must point to the Portal sharing/rest/generateToken endpoint.");
        }

        if (ServerUrl is null) {
            throw new InvalidOperationException("ServerUrl must be configured.");
        }

        if (!ServerUrl.IsAbsoluteUri) {
            throw new InvalidOperationException("ServerUrl must be an absolute URI.");
        }

        if (RequestTimeout <= TimeSpan.Zero) {
            throw new InvalidOperationException("RequestTimeout must be greater than zero.");
        }

        if (RefreshBeforeExpiration < TimeSpan.Zero) {
            throw new InvalidOperationException(
                "RefreshBeforeExpiration must be greater than or equal to zero.");
        }
    }
}