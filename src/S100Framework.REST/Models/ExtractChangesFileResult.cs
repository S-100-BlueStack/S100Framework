namespace S100Framework.REST.Models;

public sealed record ExtractChangesFileResult(
    byte[] Content,
    string? ContentType,
    string? FileName,
    Uri ResultUrl);