namespace S100Framework.REST.Models;

/// <summary>
/// Represents downloaded attachment content.
/// </summary>
/// <param name="Content">
/// The raw attachment bytes.
/// </param>
/// <param name="ContentType">
/// The attachment MIME type, when available.
/// </param>
/// <param name="FileName">
/// The attachment file name, when available.
/// </param>
public sealed record AttachmentContent(
    byte[] Content,
    string? ContentType,
    string? FileName);