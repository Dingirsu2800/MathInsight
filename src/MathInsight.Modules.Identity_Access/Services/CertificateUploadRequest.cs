namespace MathInsight.Modules.Identity_Access.Services;

/// <summary>
/// A teacher certificate to upload at registration time (BR-05): JPEG or PNG, ≤ 10 MB.
/// <paramref name="SizeInBytes"/> is the declared content length (e.g. <c>IFormFile.Length</c>),
/// carried explicitly so the size limit can be checked before the stream is buffered.
/// </summary>
public sealed record CertificateUploadRequest(
    Stream Content,
    string FileName,
    string ContentType,
    long SizeInBytes);
