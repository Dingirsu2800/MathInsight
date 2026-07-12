namespace MathInsight.Shared.Storage;

public sealed record ImageUploadRequest(
    Stream Content,
    string FileName,
    string ContentType,
    string Folder);
