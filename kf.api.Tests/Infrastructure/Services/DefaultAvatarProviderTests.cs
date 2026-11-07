using KnightFight.Api.Infrastructure.Services;

namespace KnightFight.Api.Tests.Infrastructure.Services;

public class DefaultAvatarProviderTests
{
    private static readonly byte[] PngSignature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    private readonly IDefaultAvatarProvider _provider = new DefaultAvatarProvider();

    [Fact]
    public void Avatars_ContainsTenPngImages()
    {
        Assert.Equal(10, _provider.Avatars.Count);

        foreach (var avatar in _provider.Avatars)
        {
            Assert.True(avatar.Length > 0);
            Assert.Equal(PngSignature, avatar.Take(PngSignature.Length).ToArray());
        }
    }

    [Fact]
    public void GetBytes_ReturnsTheAvatarForTheIndex()
    {
        Assert.Equal(_provider.Avatars[3], _provider.GetBytes(3));
    }

    [Fact]
    public void GetBytes_OutOfRange_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _provider.GetBytes(10));
        Assert.Throws<ArgumentOutOfRangeException>(() => _provider.GetBytes(-1));
    }

    [Fact]
    public void RandomIndex_StaysInRange()
    {
        for (var i = 0; i < 500; i++)
        {
            Assert.InRange(_provider.RandomIndex(), 0, 9);
        }
    }
}
