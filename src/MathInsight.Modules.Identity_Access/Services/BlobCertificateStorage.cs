using MathInsight.Shared.Storage;

namespace MathInsight.Modules.Identity_Access.Services;

/// <summary>
/// Certificate storage backed by the shared blob/image storage abstraction
/// (<see cref="IImageStorage"/>, Cloudinary in this project). Reusing the shared storage keeps
/// upload configuration and credentials in one place rather than introducing a parallel client.
/// Enforces the BR-05 constraints (accepted types, ≤ 10 MB) before delegating; the shared
/// <c>/auto/upload</c> endpoint lets Cloudinary classify each file itself, so images and documents
/// travel the same path.
/// </summary>
public class BlobCertificateStorage : ICertificateStorage
{
    private const string CertificateFolder = "teacher-certificates";
    private const long MaxSizeInBytes = 10L * 1024 * 1024; // 10 MB per file (BR-05)

    /// <summary>Rendered inline as a thumbnail by the review screens.</summary>
    private static readonly HashSet<string> ImageContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
    };

    private static readonly HashSet<string> DocumentContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf",
        "application/msword",                                                       // .doc
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",  // .docx
    };

    /// <summary>
    /// Browsers are unreliable about the content type of .doc/.docx (Windows in particular sends
    /// application/octet-stream or a legacy CDF type depending on how the file was created), so a
    /// recognised extension is accepted as an alternative signal. Neither check proves the bytes
    /// are what they claim; both exist to keep obvious mistakes out, and the file is only ever
    /// handed back as a download link.
    /// </summary>
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg",
        ".jpeg",
        ".png",
        ".pdf",
        ".doc",
        ".docx",
    };

    private readonly IImageStorage _storage;

    public BlobCertificateStorage(IImageStorage storage)
    {
        _storage = storage;
    }

    public Task<string> UploadAsync(CertificateUploadRequest request, CancellationToken cancellationToken)
    {
        var contentType = request.ContentType?.Trim() ?? string.Empty;
        var extension = Path.GetExtension(request.FileName)?.Trim() ?? string.Empty;

        var isImage = ImageContentTypes.Contains(contentType);
        var isAccepted = isImage
            || DocumentContentTypes.Contains(contentType)
            || AllowedExtensions.Contains(extension);

        if (!isAccepted)
        {
            throw new UnsupportedCertificateTypeException(request.ContentType, request.FileName);
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
            CertificateFolder,
            // Word documents land in Cloudinary's raw bucket and would otherwise be served from an
            // extension-less URL, which the review screens use to tell a document from an image.
            PreserveFileExtension: !isImage);

        return _storage.UploadAsync(uploadRequest, cancellationToken);
    }
}
