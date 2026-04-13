using System.Net;

namespace S100Framework.REST.Exceptions;

public sealed class FeatureServiceException : Exception
{
    public FeatureServiceException(
        string message,
        Uri requestUri,
        int? errorCode = null,
        IReadOnlyList<string>? details = null,
        HttpStatusCode? statusCode = null,
        Exception? innerException = null)
        : base(CreateMessage(message, requestUri, errorCode, details, statusCode), innerException) {
        RequestUri = requestUri;
        ErrorCode = errorCode;
        StatusCode = statusCode;
        Details = details ?? Array.Empty<string>();
    }

    public Uri RequestUri { get; }

    public int? ErrorCode { get; }

    public HttpStatusCode? StatusCode { get; }

    public IReadOnlyList<string> Details { get; }

    private static string CreateMessage(
        string message,
        Uri requestUri,
        int? errorCode,
        IReadOnlyList<string>? details,
        HttpStatusCode? statusCode) {
        var parts = new List<string>();

        if (statusCode is not null) {
            parts.Add($"HTTP {(int)statusCode} ({statusCode})");
        }

        if (errorCode is not null) {
            parts.Add($"Esri error code {errorCode}");
        }

        parts.Add(message);
        parts.Add($"Request: {requestUri}");

        if (details is { Count: > 0 }) {
            parts.Add($"Details: {string.Join(" | ", details)}");
        }

        return string.Join(". ", parts);
    }
}