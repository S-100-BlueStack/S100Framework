using System.Text.Json;
using S100Framework.REST.Abstractions;
using S100Framework.REST.Exceptions;

namespace S100Framework.REST.Authorization;

/// <summary>
/// Exchanges a portal access token for a federated ArcGIS Server token and refreshes
/// it when needed.
/// </summary>
/// <remarks>
/// This provider caches the exchanged server token and reuses it until it enters the
/// configured refresh window.
/// </remarks>
public sealed class PortalServerTokenExchangeProvider : IFeatureServiceAccessTokenProvider, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new() {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly IFeatureServiceAccessTokenProvider _portalTokenProvider;
    private readonly PortalServerTokenExchangeOptions _options;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    private FeatureServiceAccessToken? _cachedToken;
    private bool _disposed;

    /// <summary>
    /// Initializes the provider.
    /// </summary>
    /// <param name="httpClient">
    /// The HTTP client used to call the portal token exchange endpoint.
    /// </param>
    /// <param name="portalTokenProvider">
    /// The provider that supplies portal access tokens.
    /// </param>
    /// <param name="options">
    /// The portal-to-server token exchange options.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when any constructor argument is <see langword="null" />.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <paramref name="options" /> is invalid.
    /// </exception>
    public PortalServerTokenExchangeProvider(
        HttpClient httpClient,
        IFeatureServiceAccessTokenProvider portalTokenProvider,
        PortalServerTokenExchangeOptions options) {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _portalTokenProvider = portalTokenProvider ?? throw new ArgumentNullException(nameof(portalTokenProvider));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _options.Validate();
    }

    /// <summary>
    /// Gets a valid ArcGIS Server token obtained through portal token exchange.
    /// </summary>
    /// <param name="cancellationToken">
    /// A token that can be used to cancel token acquisition.
    /// </param>
    /// <returns>
    /// A reusable cached server token, or a newly exchanged token when refresh is required.
    /// </returns>
    /// <exception cref="ObjectDisposedException">
    /// Thrown when the provider has already been disposed.
    /// </exception>
    /// <exception cref="FeatureServiceAuthenticationException">
    /// Thrown when the portal token or exchanged server token cannot be obtained.
    /// </exception>
    public async ValueTask<FeatureServiceAccessToken> GetAccessTokenAsync(
        CancellationToken cancellationToken = default) {
        ThrowIfDisposed();

        var utcNow = DateTimeOffset.UtcNow;
        if (TryGetReusableToken(utcNow, out var cachedToken)) {
            return cachedToken;
        }

        await _refreshLock.WaitAsync(cancellationToken);
        try {
            utcNow = DateTimeOffset.UtcNow;
            if (TryGetReusableToken(utcNow, out cachedToken)) {
                return cachedToken;
            }

            _cachedToken = await ExchangeTokenAsync(cancellationToken);
            return _cachedToken;
        }
        finally {
            _refreshLock.Release();
        }
    }

    /// <summary>
    /// Releases resources held by the provider.
    /// </summary>
    public void Dispose() {
        if (_disposed) {
            return;
        }

        _refreshLock.Dispose();
        _disposed = true;
    }

    private bool TryGetReusableToken(
        DateTimeOffset utcNow,
        out FeatureServiceAccessToken accessToken) {
        accessToken = default!;

        if (_cachedToken is null) {
            return false;
        }

        if (_cachedToken.ShouldRefresh(utcNow, _options.RefreshBeforeExpiration)) {
            return false;
        }

        accessToken = _cachedToken;
        return true;
    }

    private async Task<FeatureServiceAccessToken> ExchangeTokenAsync(
        CancellationToken cancellationToken) {
        var portalAccessToken = await _portalTokenProvider.GetAccessTokenAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(portalAccessToken.Token)) {
            throw new FeatureServiceAuthenticationException(
                "The portal access token provider returned an empty token.",
                _options.GenerateTokenUri);
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(_options.RequestTimeout);

        using var request = new HttpRequestMessage(HttpMethod.Post, _options.GenerateTokenUri) {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>(StringComparer.Ordinal) {
                ["token"] = portalAccessToken.Token,
                ["serverUrl"] = _options.ServerUrl!.AbsoluteUri.TrimEnd('/'),
                ["f"] = "json"
            })
        };

        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            timeoutCts.Token);

        var payload = await response.Content.ReadAsStringAsync(timeoutCts.Token);
        if (string.IsNullOrWhiteSpace(payload)) {
            throw new FeatureServiceAuthenticationException(
                "The portal token exchange endpoint returned an empty payload.",
                _options.GenerateTokenUri,
                response.StatusCode);
        }

        ExchangeTokenResponse? result;
        try {
            result = JsonSerializer.Deserialize<ExchangeTokenResponse>(payload, JsonOptions);
        }
        catch (JsonException exception) {
            throw new FeatureServiceAuthenticationException(
                "The portal token exchange payload could not be deserialized.",
                _options.GenerateTokenUri,
                response.StatusCode,
                exception);
        }

        if (result?.Error is not null) {
            throw new FeatureServiceAuthenticationException(
                CreateErrorMessage(result.Error),
                _options.GenerateTokenUri,
                response.StatusCode);
        }

        if (!response.IsSuccessStatusCode) {
            throw new FeatureServiceAuthenticationException(
                $"The portal token exchange endpoint returned HTTP {(int)response.StatusCode} ({response.StatusCode}).",
                _options.GenerateTokenUri,
                response.StatusCode);
        }

        if (string.IsNullOrWhiteSpace(result?.Token)) {
            throw new FeatureServiceAuthenticationException(
                "The portal token exchange response did not contain a token.",
                _options.GenerateTokenUri,
                response.StatusCode);
        }

        if (result.Expires is null) {
            throw new FeatureServiceAuthenticationException(
                "The portal token exchange response did not contain an expires value.",
                _options.GenerateTokenUri,
                response.StatusCode);
        }

        return new FeatureServiceAccessToken(
            result.Token,
            DateTimeOffset.FromUnixTimeMilliseconds(result.Expires.Value));
    }

    private static string CreateErrorMessage(ExchangeTokenError error) {
        var parts = new List<string>();

        if (error.Code.HasValue) {
            parts.Add($"Code {error.Code.Value}");
        }

        if (!string.IsNullOrWhiteSpace(error.Message)) {
            parts.Add(error.Message);
        }

        var details = NormalizeErrorDetails(error.Details);

        if (details.Count > 0) {
            parts.Add(string.Join(" | ", details));
        }

        return parts.Count > 0
            ? $"The portal token exchange endpoint returned an authentication error. {string.Join(" - ", parts)}"
            : "The portal token exchange endpoint returned an authentication error.";
    }

    private static IReadOnlyList<string> NormalizeErrorDetails(
    IEnumerable<string?>? details) {
        return details?
            .Where(static detail => !string.IsNullOrWhiteSpace(detail))
            .Select(static detail => detail!)
            .ToArray() ?? Array.Empty<string>();
    }

    private void ThrowIfDisposed() {
        if (_disposed) {
            throw new ObjectDisposedException(nameof(PortalServerTokenExchangeProvider));
        }
    }

    private sealed record ExchangeTokenResponse(
        string? Token,
        long? Expires,
        bool? Ssl,
        ExchangeTokenError? Error);

    private sealed record ExchangeTokenError(
     int? Code,
     string? Message,
     IReadOnlyList<string?>? Details);
}