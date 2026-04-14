namespace S100Framework.REST.Models;

public sealed record AddAttachmentResult(
    AttachmentEditResult Result,
    long? EditMoment);