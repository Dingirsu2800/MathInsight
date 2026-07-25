namespace MathInsight.Modules.Identity_Access.Services;

/// <summary>
/// Uploads teacher certificates to durable storage at registration time and returns the URL
/// held in the Redis pending-registration payload (BR-05). The TeacherApplication row that
/// references the URL is created only later, at email confirmation.
/// </summary>
public interface ICertificateStorage
{
    /// <summary>Uploads the certificate and returns its absolute URL.</summary>
    Task<string> UploadAsync(CertificateUploadRequest request, CancellationToken cancellationToken);
}
