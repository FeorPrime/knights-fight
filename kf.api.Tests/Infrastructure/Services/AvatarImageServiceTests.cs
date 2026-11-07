using KnightFight.Api.Infrastructure.Services.Images;
using SkiaSharp;

namespace KnightFight.Api.Tests.Infrastructure.Services;

public class AvatarImageServiceTests
{
    private readonly IImageService _service = new AvatarImageService();

    private static byte[] MakeImage(int width, int height, SKEncodedImageFormat format)
    {
        using var bitmap = new SKBitmap(width, height);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.DodgerBlue);
        }

        using var data = bitmap.Encode(format, 90);
        return data.ToArray();
    }

    private static (int Width, int Height) SizeOf(byte[] png)
    {
        using var codec = SKCodec.Create(new SKMemoryStream(png));
        return (codec.Info.Width, codec.Info.Height);
    }

    [Fact]
    public async Task NormalizeAsync_SquarePng_CropsTo50x50Png()
    {
        var input = MakeImage(200, 200, SKEncodedImageFormat.Png);

        var result = await _service.NormalizeAsync(new MemoryStream(input));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Bytes);
        Assert.Equal((50, 50), SizeOf(result.Bytes!));
    }

    [Fact]
    public async Task NormalizeAsync_NonSquareJpeg_CenterCropsTo50x50Png()
    {
        var input = MakeImage(800, 400, SKEncodedImageFormat.Jpeg);

        var result = await _service.NormalizeAsync(new MemoryStream(input));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Bytes);
        Assert.Equal((50, 50), SizeOf(result.Bytes!));
    }

    [Fact]
    public async Task NormalizeAsync_EmptyStream_ReturnsEmpty()
    {
        var result = await _service.NormalizeAsync(new MemoryStream());

        Assert.False(result.IsSuccess);
        Assert.Equal(AvatarImageError.Empty, result.Error);
    }

    [Fact]
    public async Task NormalizeAsync_NotAnImage_ReturnsUnsupportedFormat()
    {
        var garbage = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08 };

        var result = await _service.NormalizeAsync(new MemoryStream(garbage));

        Assert.False(result.IsSuccess);
        Assert.Equal(AvatarImageError.UnsupportedFormat, result.Error);
    }

    [Fact]
    public async Task NormalizeAsync_ImageTooLarge_ReturnsTooLarge()
    {
        var input = MakeImage(4097, 100, SKEncodedImageFormat.Png);

        var result = await _service.NormalizeAsync(new MemoryStream(input));

        Assert.False(result.IsSuccess);
        Assert.Equal(AvatarImageError.TooLarge, result.Error);
    }
}
