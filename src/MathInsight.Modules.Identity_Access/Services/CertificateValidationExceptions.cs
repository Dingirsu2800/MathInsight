namespace MathInsight.Modules.Identity_Access.Services;

/// <summary>
/// Thrown when a teacher certificate fails BR-05 validation (unsupported type, or too large).
/// Deliberately distinct from the shared storage's <c>ImageUploadException</c> so the Phase 3
/// registration handler can map it to a clean HTTP 400 instead of a generic storage failure.
/// </summary>
public class InvalidCertificateException : Exception
{
    public InvalidCertificateException(string message)
        : base(message)
    {
    }
}

/// <summary>The certificate's content type is not one of the accepted image types (BR-05).</summary>
public sealed class UnsupportedCertificateTypeException : InvalidCertificateException
{
    public UnsupportedCertificateTypeException(string? contentType)
        : base($"Unsupported certificate content type '{contentType}'. Only image/jpeg and image/png are accepted.")
    {
    }
}

/// <summary>The certificate exceeds the 10 MB size limit (BR-05).</summary>
public sealed class CertificateTooLargeException : InvalidCertificateException
{
    public CertificateTooLargeException(long sizeInBytes, long maxSizeInBytes)
        : base($"Certificate is {sizeInBytes} bytes, exceeding the {maxSizeInBytes}-byte (10 MB) limit.")
    {
    }
}
