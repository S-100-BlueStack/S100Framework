using System.Text;
using S100Framework.REST.Exceptions;
using S100Framework.REST.Models;

namespace S100Framework.REST.Extensions;

/// <summary>
/// Provides helpers for failing fast on replica edit result errors.
/// </summary>
public static class ReplicaJsonResultFileErrorExtensions
{
    /// <summary>
    /// Throws when the parsed replica JSON result file contains failed edit results.
    /// </summary>
    /// <param name="result">
    /// The parsed replica JSON result file.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="result" /> is <see langword="null" />.
    /// </exception>
    /// <exception cref="ReplicaEditResultsException">
    /// Thrown when one or more parsed edit results failed.
    /// </exception>
    public static void ThrowIfEditErrors(this ReplicaJsonResultFile result) {
        ArgumentNullException.ThrowIfNull(result);

        var errors = result.GetLayerEditErrors().ToArray();

        if (errors.Length == 0) {
            return;
        }

        throw new ReplicaEditResultsException(
            BuildMessage(errors),
            errors);
    }

    private static string BuildMessage(IReadOnlyList<ReplicaLayerEditResult> errors) {
        var builder = new StringBuilder();

        builder.Append("The replica JSON result file contains ");
        builder.Append(errors.Count);
        builder.Append(errors.Count == 1 ? " failed edit result." : " failed edit results.");

        foreach (var error in errors.Take(5)) {
            builder.Append(' ');
            builder.Append("Layer ");
            builder.Append(error.LayerId);
            builder.Append(' ');
            builder.Append(error.Operation);
            builder.Append(" failed");

            if (error.ObjectId.HasValue) {
                builder.Append(" for objectId ");
                builder.Append(error.ObjectId.Value);
            }

            if (!string.IsNullOrWhiteSpace(error.GlobalId)) {
                builder.Append(" globalId ");
                builder.Append(error.GlobalId);
            }

            if (error.ErrorCode.HasValue) {
                builder.Append(" with code ");
                builder.Append(error.ErrorCode.Value);
            }

            if (!string.IsNullOrWhiteSpace(error.ErrorDescription)) {
                builder.Append(": ");
                builder.Append(error.ErrorDescription);
            }

            builder.Append('.');
        }

        if (errors.Count > 5) {
            builder.Append(' ');
            builder.Append(errors.Count - 5);
            builder.Append(" additional edit errors were omitted from the message.");
        }

        return builder.ToString();
    }
}