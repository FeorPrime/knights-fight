namespace KnightFight.Api.Infrastructure.Services;

/// <summary>
/// Serves the ten default challenger avatars that ship with the API.
/// </summary>
/// <remarks>
/// The avatars are embedded resources rather than files on disk so they are
/// present in the container image regardless of the working directory.
/// A challenger that was not given a custom avatar stores an index into this
/// collection instead of a copy of the bytes.
/// </remarks>
public interface IDefaultAvatarProvider
{
    /// <summary>The ten default avatars, indexed 0..9.</summary>
    IReadOnlyList<byte[]> Avatars { get; }

    /// <summary>The PNG bytes of the avatar at <paramref name="index"/>.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The index is outside the collection.</exception>
    byte[] GetBytes(int index);

    /// <summary>A uniformly random index into <see cref="Avatars"/>.</summary>
    int RandomIndex();
}

public class DefaultAvatarProvider : IDefaultAvatarProvider
{
    public const int AvatarCount = 10;

    private const string ResourcePrefix = "KnightFight.Api.Assets.avatars.avatar_";

    private readonly byte[][] _avatars;

    public DefaultAvatarProvider()
    {
        _avatars = new byte[AvatarCount][];

        for (var i = 0; i < AvatarCount; i++)
        {
            var resourceName = $"{ResourcePrefix}{i + 1:00}.png";

            using var stream = typeof(DefaultAvatarProvider).Assembly
                                   .GetManifestResourceStream(resourceName)
                               ?? throw new InvalidOperationException(
                                   $"Embedded avatar resource '{resourceName}' was not found.");

            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            _avatars[i] = memory.ToArray();
        }
    }

    public IReadOnlyList<byte[]> Avatars => _avatars;

    public byte[] GetBytes(int index)
    {
        if (index < 0 || index >= _avatars.Length)
            throw new ArgumentOutOfRangeException(nameof(index));

        return _avatars[index];
    }

    public int RandomIndex() => Random.Shared.Next(_avatars.Length);
}
