using MathInsight.Shared.Storage;

namespace MathInsight.Modules.Identity_Access.Services;

/// <summary>
/// Certificate storage backed by the shared blob/image storage abstraction
/// (<see cref="IImageStorage"/>, Cloudinary in this project). Reusing the shared storage keeps
/// upload configuration and credentials in one place rather than introducing a parallel client.
/// Enforces the BR-05 constraints (JPEG/PNG only, ≤ 10 MB) before delegating; the shared
/// <c>/image/upload</c> endpoint is correct for JPEG and PNG.
/// </summary>
public class BlobCertificateStorage : ICertificateStorage
{
    private const string CertificateFolder = "teacher-certificates";
    private const long MaxSizeInBytes = 10L * 1024 * 1024; // 10 MB (BR-05)

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
    };

    private readonly IImageStorage _storage;

    public BlobCertificateStorage(IImageStorage storage)
    {
        _storage = storage;
    }

    public Task<string> UploadAsync(CertificateUploadRequest request, CancellationToken cancellationToken)
    {
        var contentType = request.ContentType?.Trim() ?? string.Empty;

        if (!AllowedContentTypes.Contains(contentType))
        {
            throw new UnsupportedCertificateTypeException(request.ContentType);
        }

        // Enforce the size limit up front, before the stream is buffered into memory downstream.
        if (request.SizeInBytes > MaxSizeInBytes)
        {
            throw new CertificateTooLargeException(request.SizeInBytes, MaxSizeInBytes);
        }

        var uploadRequest = new ImageUploadRequest(
            request.Content,
            request.FileName,
            contentType,
            CertificateFolder);

        return _storage.UploadAsync(uploadRequest, cancellationToken);
    }
}
