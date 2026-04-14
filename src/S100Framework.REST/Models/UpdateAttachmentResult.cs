namespace S100Framework.REST.Models;

public sealed record UpdateAttachmentResult(
    AttachmentEditResult Result,
    long? EditMoment);