namespace MathInsight.Shared.Storage;

public sealed class ImageStorageUnavailableException : Exception
{
    public ImageStorageUnavailableException()
        : base("Image storage is not configured.")
    {
    }
}

public sealed class ImageUploadException : Exception
{
    public ImageUploadException()
        : base("Image upload failed.")
    {
    }

    public ImageUploadException(string message)
        : base(message)
    {
    }

    public ImageUploadException(Exception innerException)
        : base("Image upload failed.", innerException)
    {
    }
}
