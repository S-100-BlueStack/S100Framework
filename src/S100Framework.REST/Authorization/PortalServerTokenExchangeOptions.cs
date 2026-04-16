namespace S100Framework.REST.Authorization;

public sealed class PortalServerTokenExchangeOptions
{
    public Uri? GenerateTokenUri { get; set; }

    public Uri? ServerUrl { get; set; }

    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(30);

    public TimeSpan RefreshBeforeExpiration { get; set; } = TimeSpan.FromMinutes(1);

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