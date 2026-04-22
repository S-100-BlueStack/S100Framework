namespace S100Framework.REST.Models;

/// <summary>
/// Represents the result of an update-attachment operation.
/// </summary>
/// <param name="Result">
/// The per-attachment edit result returned by the service.
/// </param>
/// <param name="EditMoment">
/// The edit moment returned by the service, when requested.
/// </param>
public sealed record UpdateAttachmentResult(
    AttachmentEditResult Result,
    long? EditMoment);