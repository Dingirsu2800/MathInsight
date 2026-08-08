namespace MathInsight.Shared.Storage;

/// <summary>
/// A file to push to the shared storage. Despite the name the backing endpoint is Cloudinary's
/// <c>auto</c> resource type, so non-image files (PDF, Word, video) upload through here too.
/// </summary>
/// <param name="PreserveFileExtension">
/// Appends <paramref name="FileName"/>'s extension to the generated public id.
///
/// Cloudinary derives the delivery extension from the detected format for images, but a file it
/// classifies as <c>raw</c> (Word documents) is served under the public id verbatim — with a bare
/// GUID public id the resulting URL carries no extension at all, so a browser cannot tell what it
/// downloaded and callers cannot infer the type from the URL. Opt in for uploads that may be
/// non-image; leave false to keep the historical URL shape for image-only callers.
/// </param>
public sealed record ImageUploadRequest(
    Stream Content,
    string FileName,
    string ContentType,
    string Folder,
    bool PreserveFileExtension = false);
