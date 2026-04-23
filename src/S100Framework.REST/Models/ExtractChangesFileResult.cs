namespace S100Framework.REST.Models;

/// <summary>
/// Represents a downloaded file produced by an asynchronous <c>extractChanges</c> job.
/// </summary>
/// <param name="Content">
/// The downloaded file content.
/// </param>
/// <param name="ContentType">
/// The HTTP content type, when provided by the server.
/// </param>
/// <param name="FileName">
/// The suggested file name, when provided by the server.
/// </param>
/// <param name="ResultUrl">
/// The URL the file was downloaded from.
/// </param>
public sealed record ExtractChangesFileResult(
    byte[] Content,
    string? ContentType,
    string? FileName,
    Uri ResultUrl);