using System.Globalization;
using System.Text.Json;
using S100Framework.REST.Abstractions;
using S100Framework.REST.Exceptions;

namespace S100Framework.REST.Authorization;

/// <summary>
/// Acquires and refreshes ArcGIS Server access tokens from a
/// <c>tokens/generateToken</c> endpoint.
/// </summary>
/// <remarks>
/// This provider caches the last successful token and refreshes it when it enters the
/// configured refresh window.
/// </remarks>
public sealed class ArcGisServerGenerateTokenProvider : IFeatureServiceAccessTokenProvider, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new() {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly ArcGisServerGenerateTokenOptions _options;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    private FeatureServiceAccessToken? _cachedToken;
    private bool _disposed;

    /// <summary>
    /// Initializes the provider.
    /// </summary>
    /// <param name="httpClient">
    /// The HTTP client used to call the ArcGIS Server token endpoint.
    /// </param>
    /// <param name="options">
    /// The token acquisition options.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="httpClient" /> or <paramref name="options" /> is
    /// <see langword="null" />.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <paramref name="options" /> is invalid.
    /// </exception>
    public ArcGisServerGenerateTokenProvider(
        HttpClient httpClient,
        ArcGisServerGenerateTokenOptions options) {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _options.Validate();
    }

    /// <summary>
    /// Gets a valid ArcGIS Server access token.
    /// </summary>
    /// <param name="cancellationToken">
    /// A token that can be used to cancel token acquisition.
    /// </param>
    /// <returns>
    /// A reusable cached token, or a newly acquired token when refresh is required.
    /// </returns>
    /// <exception cref="ObjectDisposedException">
    /// Thrown when the provider has already been disposed.
    /// </exception>
    /// <exception cref="FeatureServiceAuthenticationException">
    /// Thrown when token acquisition fails.
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

            _cachedToken = await RequestTokenAsync(cancellationToken);
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

    private async Task<FeatureServiceAccessToken> RequestTokenAsync(
        CancellationToken cancellationToken) {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(_options.RequestTimeout);

        using var request = new HttpRequestMessage(HttpMethod.Post, _options.TokenUri) {
            Content = new FormUrlEncodedContent(BuildRequestParameters())
        };

        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            timeoutCts.Token);

        var payload = await response.Content.ReadAsStringAsync(timeoutCts.Token);
        if (string.IsNullOrWhiteSpace(payload)) {
            throw new FeatureServiceAuthenticationException(
                "The token service returned an empty payload.",
                _options.TokenUri,
                response.StatusCode);
        }

        GenerateTokenResponse? result;
        try {
            result = JsonSerializer.Deserialize<GenerateTokenResponse>(payload, JsonOptions);
        }
        catch (JsonException exception) {
            throw new FeatureServiceAuthenticationException(
                "The token service payload could not be deserialized.",
                _options.TokenUri,
                response.StatusCode,
                exception);
        }

        if (result?.Error is not null) {
            throw new FeatureServiceAuthenticationException(
                CreateErrorMessage(result.Error),
                _options.TokenUri,
                response.StatusCode);
        }

        if (!response.IsSuccessStatusCode) {
            throw new FeatureServiceAuthenticationException(
                $"The token service returned HTTP {(int)response.StatusCode} ({response.StatusCode}).",
                _options.TokenUri,
                response.StatusCode);
        }

        if (string.IsNullOrWhiteSpace(result?.Token)) {
            throw new FeatureServiceAuthenticationException(
                "The token service response did not contain a token.",
                _options.TokenUri,
                response.StatusCode);
        }

        if (result.Expires is null) {
            throw new FeatureServiceAuthenticationException(
                "The token service response did not contain an expires value.",
                _options.TokenUri,
                response.StatusCode);
        }

        return new FeatureServiceAccessToken(
            result.Token,
            DateTimeOffset.FromUnixTimeMilliseconds(result.Expires.Value));
    }

    private Dictionary<string, string> BuildRequestParameters() {
        var parameters = new Dictionary<string, string>(StringComparer.Ordinal) {
            ["username"] = _options.Username!,
            ["password"] = _options.Password!,
            ["client"] = MapClientType(_options.ClientType),
            ["expiration"] = _options.ExpirationMinutes.ToString(CultureInfo.InvariantCulture),
            ["f"] = "json"
        };

        switch (_options.ClientType) {
            case ArcGisServerTokenClientType.Referer:
                parameters["referer"] = _options.Referer!;
                break;

            case ArcGisServerTokenClientType.Ip:
                parameters["ip"] = _options.IpAddress!;
                break;

            case ArcGisServerTokenClientType.RequestIp:
                break;

            default:
                throw new InvalidOperationException(
                    $"Unsupported client type '{_options.ClientType}'.");
        }

        return parameters;
    }

    private static string MapClientType(ArcGisServerTokenClientType clientType) {
        return clientType switch {
            ArcGisServerTokenClientType.Referer => "referer",
            ArcGisServerTokenClientType.Ip => "ip",
            ArcGisServerTokenClientType.RequestIp => "requestip",
            _ => throw new InvalidOperationException(
                $"Unsupported client type '{clientType}'.")
        };
    }

    private static string CreateErrorMessage(GenerateTokenError error) {
        var parts = new List<string>();

        if (error.Code.HasValue) {
            parts.Add($"Code {error.Code.Value}");
        }

        if (!string.IsNullOrWhiteSpace(error.Message)) {
            parts.Add(error.Message);
        }

        if (error.Details is { Count: > 0 }) {
            parts.Add(string.Join(
                " | ",
                error.Details.Where(static detail => !string.IsNullOrWhiteSpace(detail))));
        }

        return parts.Count > 0
            ? $"The token service returned an authentication error. {string.Join(" - ", parts)}"
            : "The token service returned an authentication error.";
    }

    private void ThrowIfDisposed() {
        if (_disposed) {
            throw new ObjectDisposedException(nameof(ArcGisServerGenerateTokenProvider));
        }
    }

    private sealed record GenerateTokenResponse(
        string? Token,
        long? Expires,
        GenerateTokenError? Error);

    private sealed record GenerateTokenError(
        int? Code,
        string? Message,
        IReadOnlyList<string>? Details);
}