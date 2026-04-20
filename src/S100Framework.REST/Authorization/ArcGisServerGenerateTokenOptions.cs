namespace S100Framework.REST.Authorization;

/// <summary>
/// Configures token acquisition against an ArcGIS Server <c>tokens/generateToken</c> endpoint.
/// </summary>
public sealed class ArcGisServerGenerateTokenOptions
{
    /// <summary>
    /// Gets or sets the ArcGIS Server token endpoint URI.
    /// </summary>
    public Uri? TokenUri { get; set; }

    /// <summary>
    /// Gets or sets the username used to request the token.
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// Gets or sets the password used to request the token.
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// Gets or sets how the token should be bound to the caller.
    /// </summary>
    public ArcGisServerTokenClientType ClientType { get; set; } =
        ArcGisServerTokenClientType.Referer;

    /// <summary>
    /// Gets or sets the referer URL used when <see cref="ClientType"/> is <see cref="ArcGisServerTokenClientType.Referer"/>.
    /// </summary>
    public string? Referer { get; set; }

    /// <summary>
    /// Gets or sets the IP address used when <see cref="ClientType"/> is <see cref="ArcGisServerTokenClientType.Ip"/>.
    /// </summary>
    public string? IpAddress { get; set; }

    /// <summary>
    /// Gets or sets the requested token lifetime in minutes.
    /// </summary>
    public int ExpirationMinutes { get; set; } = 60;

    /// <summary>
    /// Gets or sets the timeout for the token request.
    /// </summary>
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets how long before expiration the token should be refreshed.
    /// </summary>
    public TimeSpan RefreshBeforeExpiration { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Validates the configured options.
    /// </summary>
    public void Validate() {
        if (TokenUri is null) {
            throw new InvalidOperationException("TokenUri must be configured.");
        }

        if (!TokenUri.IsAbsoluteUri) {
            throw new InvalidOperationException("TokenUri must be an absolute URI.");
        }

        if (!string.Equals(TokenUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)) {
            throw new InvalidOperationException("TokenUri must use HTTPS.");
        }

        if (!TokenUri.AbsolutePath.EndsWith(
                "/tokens/generateToken",
                StringComparison.OrdinalIgnoreCase)) {
            throw new InvalidOperationException(
                "TokenUri must point to the ArcGIS Server tokens/generateToken endpoint.");
        }

        if (string.IsNullOrWhiteSpace(Username)) {
            throw new InvalidOperationException("Username must be configured.");
        }

        if (string.IsNullOrWhiteSpace(Password)) {
            throw new InvalidOperationException("Password must be configured.");
        }

        if (ExpirationMinutes <= 0) {
            throw new InvalidOperationException(
                "ExpirationMinutes must be greater than zero.");
        }

        if (RequestTimeout <= TimeSpan.Zero) {
            throw new InvalidOperationException(
                "RequestTimeout must be greater than zero.");
        }

        if (RefreshBeforeExpiration < TimeSpan.Zero) {
            throw new InvalidOperationException(
                "RefreshBeforeExpiration must be greater than or equal to zero.");
        }

        switch (ClientType) {
            case ArcGisServerTokenClientType.Referer:
                if (string.IsNullOrWhiteSpace(Referer)) {
                    throw new InvalidOperationException(
                        "Referer must be configured when ClientType is Referer.");
                }

                if (!Uri.TryCreate(Referer, UriKind.Absolute, out _)) {
                    throw new InvalidOperationException(
                        "Referer must be an absolute URI when ClientType is Referer.");
                }

                break;

            case ArcGisServerTokenClientType.Ip:
                if (string.IsNullOrWhiteSpace(IpAddress)) {
                    throw new InvalidOperationException(
                        "IpAddress must be configured when ClientType is Ip.");
                }

                break;

            case ArcGisServerTokenClientType.RequestIp:
                break;

            default:
                throw new InvalidOperationException(
                    $"Unsupported client type '{ClientType}'.");
        }
    }
}