namespace S100Framework.REST.Models;

/// <summary>
/// Represents a downloaded file produced by a <c>synchronizeReplica</c> result URL.
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
public sealed record SynchronizeReplicaFileResult(
    byte[] Content,
    string? ContentType,
    string? FileName,
    Uri ResultUrl);