using SkiaSharp;

namespace KnightFight.Api.Infrastructure.Services.Images;

/// <summary>
/// Turns an uploaded image into the 50x50 PNG blob the API stores.
/// </summary>
/// <remarks>
/// The format is decided by the file's magic bytes, never by the request's
/// Content-Type header, because that header is chosen by the caller.
/// </remarks>
public class AvatarImageService : IImageService
{
    public const int AvatarSize = 50;
    public const int MaxDimension = 4096;

    private static readonly byte[] PngSignature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
    private static readonly byte[] JpegSignature = { 0xFF, 0xD8, 0xFF };

    public Task<AvatarImageResult> NormalizeAsync(Stream input, CancellationToken cancellationToken = default)
    {
        if (input is null || !input.CanRead)
            return Task.FromResult(AvatarImageResult.Failure(AvatarImageError.Empty));

        using var buffer = new MemoryStream();
        input.CopyTo(buffer);
        var bytes = buffer.ToArray();

        if (bytes.Length == 0)
            return Task.FromResult(AvatarImageResult.Failure(AvatarImageError.Empty));

        if (!HasSupportedSignature(bytes))
            return Task.FromResult(AvatarImageResult.Failure(AvatarImageError.UnsupportedFormat));

        using var codec = SKCodec.Create(new SKMemoryStream(bytes));
        if (codec is null)
            return Task.FromResult(AvatarImageResult.Failure(AvatarImageError.DecodeFailed));

        // the guard reads the header, so a small file cannot expand into a huge bitmap
        var info = codec.Info;
        if (info.Width > MaxDimension || info.Height > MaxDimension)
            return Task.FromResult(AvatarImageResult.Failure(AvatarImageError.TooLarge));

        using var bitmap = SKBitmap.Decode(codec);
        if (bitmap is null)
            return Task.FromResult(AvatarImageResult.Failure(AvatarImageError.DecodeFailed));

        var encoded = EncodeSquare(bitmap, info.Width, info.Height);

        return Task.FromResult(encoded is null
            ? AvatarImageResult.Failure(AvatarImageError.DecodeFailed)
            : AvatarImageResult.Success(encoded));
    }

    private static bool HasSupportedSignature(byte[] bytes)
    {
        if (bytes.Length >= PngSignature.Length &&
            bytes.Take(PngSignature.Length).SequenceEqual(PngSignature))
            return true;

        if (bytes.Length >= JpegSignature.Length &&
            bytes.Take(JpegSignature.Length).SequenceEqual(JpegSignature))
            return true;

        return false;
    }

    /// <summary>
    /// Crops the image to its central square, scales it to
    /// <see cref="AvatarSize"/> and encodes it as PNG.
    /// </summary>
    private static byte[]? EncodeSquare(SKBitmap bitmap, int width, int height)
    {
        var side = Math.Min(width, height);
        var source = new SKRect(
            (width - side) / 2f, (height - side) / 2f,
            (width + side) / 2f, (height + side) / 2f);
        var target = new SKRect(0, 0, AvatarSize, AvatarSize);

        using var output = new SKBitmap(AvatarSize, AvatarSize);

        using (var canvas = new SKCanvas(output))
        {
            canvas.Clear(SKColors.Transparent);
            using var paint = new SKPaint
            {
                IsAntialias = true,
                FilterQuality = SKFilterQuality.High
            };
            canvas.DrawBitmap(bitmap, source, target, paint);
        }

        using var image = SKImage.FromBitmap(output);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);

        return data?.ToArray();
    }
}
