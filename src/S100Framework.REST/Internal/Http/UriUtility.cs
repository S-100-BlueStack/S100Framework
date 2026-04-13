using System.Text;

namespace S100Framework.REST.Internal.Http;

internal static class UriUtility
{
    public static Uri AppendPath(Uri baseUri, string relativePath) {
        var left = baseUri.AbsoluteUri.TrimEnd('/');
        var right = relativePath.TrimStart('/');

        return new Uri($"{left}/{right}", UriKind.Absolute);
    }

    public static Uri WithQuery(Uri baseUri, IReadOnlyDictionary<string, string?> parameters) {
        var builder = new StringBuilder();
        var first = true;

        foreach (var pair in parameters) {
            if (string.IsNullOrWhiteSpace(pair.Value)) {
                continue;
            }

            builder.Append(first ? '?' : '&');
            builder.Append(Uri.EscapeDataString(pair.Key));
            builder.Append('=');
            builder.Append(Uri.EscapeDataString(pair.Value));
            first = false;
        }

        return new Uri(baseUri.AbsoluteUri + builder, UriKind.Absolute);
    }
}