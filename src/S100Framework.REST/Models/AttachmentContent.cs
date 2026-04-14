namespace S100Framework.REST.Models;

public sealed record AttachmentContent(
    byte[] Content,
    string? ContentType,
    string? FileName);