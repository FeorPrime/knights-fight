namespace KnightFight.Api.Infrastructure.Services.Images;

/// <summary>Why an avatar image could not be turned into a stored avatar.</summary>
public enum AvatarImageError
{
    None,
    UnsupportedFormat,
    TooLarge,
    DecodeFailed,
    Empty
}

/// <summary>The outcome of normalizing an uploaded image.</summary>
public record AvatarImageResult(bool IsSuccess, byte[]? Bytes, AvatarImageError Error)
{
    public static AvatarImageResult Success(byte[] bytes) => new(true, bytes, AvatarImageError.None);

    public static AvatarImageResult Failure(AvatarImageError error) => new(false, null, error);
}

/// <summary>Turns uploaded images into the avatar blobs the API stores.</summary>
public interface IImageService
{
    /// <summary>Validates, crops and resizes an uploaded image into a 50x50 PNG.</summary>
    Task<AvatarImageResult> NormalizeAsync(Stream input, CancellationToken cancellationToken = default);
}
