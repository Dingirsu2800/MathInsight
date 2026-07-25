namespace MathInsight.Shared.Storage;

public interface IImageStorage
{
    Task<string> UploadAsync(
        ImageUploadRequest request,
        CancellationToken cancellationToken);
}
