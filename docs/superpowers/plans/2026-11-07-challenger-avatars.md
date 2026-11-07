# Challenger Avatars Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let a challenger pick a PNG/JPG avatar, shrunk to 50×50 and stored in the database as a blob; assign a random one of ten bundled defaults when they pick nothing; and show the avatar on the leaderboard and in the desktop client.

**Architecture:** The avatar travels on its own request — `POST api/challengers/{id}/avatar`, multipart — because the existing encrypted create envelope caps its plaintext at 190 bytes (RSA-2048 OAEP-SHA256) and cannot carry an image. The server normalizes the upload to a 50×50 PNG with SkiaSharp and stores it in a `bytea` column; a challenger with no custom avatar points at one of ten embedded PNG assets by index. The upload route is the API's second `[Authorize]` endpoint: the existing JWT already carries the challenger id in its `sub` claim, so ownership is checked for free.

**Tech Stack:** ASP.NET Core 10 minimal API, EF Core 10 + Npgsql, SkiaSharp 4.155.0, xUnit, React 19 + Vite 8 + Vitest, C++23 + cpr 1.10.5.

**Spec:** [`docs/superpowers/specs/2026-11-07-challenger-avatars-design.md`](../specs/2026-11-07-challenger-avatars-design.md)

## Global Constraints

- The stored avatar is **always a 50×50 PNG**, whatever the upload format was.
- Accepted upload formats: **PNG and JPG only**. Detect them by magic bytes (`89 50 4E 47 0D 0A 1A 0A` / `FF D8 FF`); **never** trust the `Content-Type` header.
- Reject images whose source dimensions exceed **4096×4096** — checked from `SKCodec.Info` *before* decoding pixels.
- Reject uploads larger than **5 MB**.
- There are exactly **10** default avatars, indexed **0–9**.
- The default avatar is assigned **when the challenger row is created**.
- `POST api/challengers/{id}/avatar` requires a Bearer JWT whose `sub` claim equals `{id}`.
- Avatar URLs are **root-relative**: `/api/challengers/{id}/avatar` (not path-relative — it must resolve on any SPA route).
- C# namespaces are `KnightFight.Api` and `KnightFight.Api.Tests` — **not** the folder names. Use `KnightFight.Api.Tests.*` for new test files; leave the existing `kf.api.Tests.*` files alone.
- API code style: C# 12, file-scoped namespaces, primary constructors, `Nullable` enabled, `ImplicitUsings` on.
- Web UI style: TypeScript strict, `verbatimModuleSyntax`, **4-space indent**, **single quotes**, Tailwind utilities inline, `cn()` for conditional classes.
- Client style: C++23, `std::expected` for fallible returns, `namespace` blocks indented inside the namespace.
- **POSTGRES_PW must be present in the shell environment** for the EF migration step (Task 3). It lives in `.env`, which must not be read or written — export it in the shell instead.
- All tests must pass before opening a PR: `dotnet test kf.api.Tests`, `cd kf.web_ui && bun run test`, `bun run build`, `bun run lint`.

## Before You Start

The feature ships as its own branch so it can be merged or dropped independently. The working tree
currently carries **uncommitted work on `feature/knights_fight`** — the client's CLI/UDP layer
(`kf.client/include/cli/`, `kf.client/include/udp/`, `kf.client/src/cli/`, `kf.client/src/udp/`,
`kf.client/tests/test_udp.cpp` and the modified `CMakeLists.txt`, `main.cpp`, `AppConfig.h`) — so:

1. Commit or stash that work first — do not sweep it into the avatar commits. **Task 9 depends on
   it**: the client already dispatches through `cli::CliApp`, and the plan adds the `avatar` command
   there rather than to `main.cpp`.
2. Then create the feature branch from the tip of `feature/knights_fight`:

```bash
cd /Users/feor/projects/students/TOPAI/Practice/knights_fight
git switch -c feature/challenger-avatars
git status --short   # must be empty apart from what you intend to carry
```

3. Every task below commits only the files it names. Check `git status --short` before each commit.

The repo root is one level up (`/Users/feor/projects/students/TOPAI/Practice`), so from here the git paths are `knights_fight/...`.

---

## File Structure

**API — new files**

| File | Responsibility |
|---|---|
| `kf.api/Assets/avatars/avatar_01.png` … `avatar_10.png` | The ten default avatars. Committed binaries. |
| `kf.api/Assets/avatars/generate_avatars.py` | Stdlib-only regenerator for those ten PNGs. |
| `kf.api/Infrastructure/Services/DefaultAvatarProvider.cs` | Loads the ten embedded assets; owns `RandomIndex()`. |
| `kf.api/Infrastructure/Services/Images/IImageService.cs` | `IImageService`, `AvatarImageResult`, `AvatarImageError`. |
| `kf.api/Infrastructure/Services/Images/AvatarImageService.cs` | Magic-byte check → dimension guard → crop → 50×50 → PNG encode. |
| `kf.api/Models/Mappers/AvatarUrl.cs` | Builds `/api/challengers/{id}/avatar`. |
| `kf.api/Features/Challenger/ChallengerAvatarGet.cs` | Serves the avatar PNG. |
| `kf.api/Features/Challenger/ChallengerAvatarUpload.cs` | Accepts the multipart upload, stores it. |

**API — modified files**

| File | Change |
|---|---|
| `kf.api/kf.api.csproj` | SkiaSharp package; embed the avatar PNGs. |
| `kf.api/Program.cs` | Register `IDefaultAvatarProvider` and `IImageService`. |
| `kf.api/Infrastructure/ApiMap.cs` | Add `ApiMap.Challenger.AVATAR`. |
| `kf.api/Models/Entities/ChallengerModel.cs` | Add `AvatarBlob`, `DefaultAvatarIndex`. |
| `kf.api/Models/Mappers/ChallengerMapper.cs` | `ToEntity` takes the default avatar index. |
| `kf.api/Features/Challenger/ChallengerCreate.cs` | Assign the default at creation. |
| `kf.api/Features/Challenger/ChallengerGetAll.cs` | Fill `AvatarUrl`. |
| `kf.api/Features/Leaderboard/LeaderboardGet.cs` | Fill `AvatarUrl`. |

**API — new migration** `kf.api/Migrations/<timestamp>_AddChallengerAvatar.cs` + the regenerated snapshot.

**API tests — new files**

| File | Responsibility |
|---|---|
| `kf.api.Tests/Infrastructure/Services/DefaultAvatarProviderTests.cs` | Ten assets load; index bounds. |
| `kf.api.Tests/Infrastructure/Services/AvatarImageServiceTests.cs` | Format gate, dimension guard, 50×50 output. |
| `kf.api.Tests/Infrastructure/EncryptedRequestFactory.cs` | Posts an RSA-encrypted body, so contract tests can drive `POST api/challenger`. |
| `kf.api.Tests/AvatarContractTests.cs` | The avatar routes end to end. |

**Web UI — modified files**

| File | Change |
|---|---|
| `kf.web_ui/src/widgets/leaderboard/LeaderboardTable.tsx` | Render `<img>` when `avatarUrl` is set. |
| `kf.web_ui/src/widgets/newcomers/NewcomersList.tsx` | Same, at 32px. |
| `kf.web_ui/src/widgets/leaderboard/LeaderboardTable.test.tsx` | New — covers both branches. |

**Client — new/modified files**

| File | Change |
|---|---|
| `kf.client/include/http/IHttpClient.h` | `MultipartFile` struct; `postMultipart`. |
| `kf.client/include/http/CprHttpClient.h` / `src/http/CprHttpClient.cpp` | Implement `postMultipart` with cpr. |
| `kf.client/include/crypto/Base64.h` / `src/crypto/Base64.cpp` | `base64UrlDecode`. |
| `kf.client/include/crypto/JwtPayload.h` / `src/crypto/JwtPayload.cpp` | `extractSubject(jwt)` → challenger id. |
| `kf.client/include/services/AvatarService.h` / `src/services/AvatarService.cpp` | Read the file, post it. |
| `kf.client/include/services/ChallengerService.h` / `src/services/ChallengerService.cpp` | `parseChallengerId`. |
| `kf.client/include/cfg/AppConfig.h` | `challengerAvatarUrl`. |
| `kf.client/include/cli/CliApp.h` / `src/cli/CliApp.cpp` | `avatar` command; `create` gains an optional avatar. |
| `kf.client/CMakeLists.txt` | Register the new sources. |

---

### Task 1: Default avatar assets and provider

**Files:**
- Create: `kf.api/Assets/avatars/avatar_01.png` … `kf.api/Assets/avatars/avatar_10.png`
- Create: `kf.api/Assets/avatars/generate_avatars.py`
- Create: `kf.api/Infrastructure/Services/DefaultAvatarProvider.cs`
- Modify: `kf.api/kf.api.csproj`
- Modify: `kf.api/Program.cs`
- Test: `kf.api.Tests/Infrastructure/Services/DefaultAvatarProviderTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces:
  ```csharp
  namespace KnightFight.Api.Infrastructure.Services;

  public interface IDefaultAvatarProvider
  {
      IReadOnlyList<byte[]> Avatars { get; }
      byte[] GetBytes(int index);
      int RandomIndex();
  }

  public class DefaultAvatarProvider : IDefaultAvatarProvider
  {
      public const int AvatarCount = 10;
      // ...
  }
  ```

- [ ] **Step 1: Write the asset generator and the ten PNGs**

Create `kf.api/Assets/avatars/generate_avatars.py`. It must use only the Python standard library (`zlib`, `struct`) so it runs anywhere Python 3 runs — there is no PIL or ImageMagick in this environment.

```python
#!/usr/bin/env python3
"""Generate the ten default challenger avatars.

Dependency-free: it writes PNG files with nothing but the Python standard
library (zlib + struct), so it runs anywhere Python 3 runs.

Every avatar is a 50x50 emblem drawn with a handful of polygons on a solid
background, so the set stays small and consistent. Shapes are drawn at 3x
scale and box-filtered down, which is what gives the edges their antialiasing.

All shape helpers take coordinates in a 0..SIZE space; the supersampling
happens inside them.

Run from the repository root:

    python3 kf.api/Assets/avatars/generate_avatars.py

The generated PNGs are committed next to this script.
"""

import struct
import zlib
from pathlib import Path

SIZE = 50          # final avatar size in pixels
SCALE = 3          # supersampling factor used while drawing
GRID = SIZE * SCALE

OUT_DIR = Path(__file__).resolve().parent

# background, foreground pairs - one per avatar
PALETTE = [
    ((31, 41, 55), (226, 232, 240)),    # 01 sword    - slate / silver
    ((30, 58, 138), (191, 219, 254)),    # 02 shield  - blue / ice
    ((120, 53, 15), (254, 215, 170)),    # 03 helmet  - brown / sand
    ((21, 94, 117), (165, 243, 252)),    # 04 spear   - teal / cyan
    ((120, 53, 15), (252, 211, 77)),     # 05 crown   - brown / gold
    ((136, 19, 55), (254, 205, 211)),    # 06 heart   - crimson / pink
    ((76, 29, 149), (221, 214, 254)),   # 07 ring    - violet / lilac
    ((113, 63, 18), (253, 230, 138)),    # 08 bolt    - amber / yellow
    ((30, 41, 55), (148, 163, 184)),    # 09 hammer  - slate / steel
    ((6, 78, 59), (167, 243, 208)),     # 10 star    - emerald / mint
]


def new_canvas(background):
    return [[background] * GRID for _ in range(GRID)]


def fill_polygon(canvas, points, color):
    """Scanline fill of a closed polygon. Points are in 0..SIZE coordinates."""
    scaled = [(x * SCALE, y * SCALE) for x, y in points]
    ys = [p[1] for p in scaled]
    y_min = max(0, int(min(ys)))
    y_max = min(GRID - 1, int(max(ys)) + 1)

    for y in range(y_min, y_max + 1):
        sample_y = y + 0.5
        crossings = []
        for i in range(len(scaled)):
            x1, y1 = scaled[i]
            x2, y2 = scaled[(i + 1) % len(scaled)]
            if (y1 <= sample_y < y2) or (y2 <= sample_y < y1):
                t = (sample_y - y1) / (y2 - y1)
                crossings.append(x1 + t * (x2 - x1))
        crossings.sort()

        for i in range(0, len(crossings) - 1, 2):
            x_start = max(0, int(round(crossings[i])))
            x_end = min(GRID - 1, int(round(crossings[i + 1])))
            for x in range(x_start, x_end + 1):
                canvas[y][x] = color


def fill_circle(canvas, cx, cy, radius, color):
    """Filled circle. Centre and radius are in 0..SIZE coordinates."""
    center_x = cx * SCALE
    center_y = cy * SCALE
    r = radius * SCALE
    for y in range(max(0, int(center_y - r)), min(GRID, int(center_y + r) + 1)):
        for x in range(max(0, int(center_x - r)), min(GRID, int(center_x + r) + 1)):
            if (x + 0.5 - center_x) ** 2 + (y + 0.5 - center_y) ** 2 <= r * r:
                canvas[y][x] = color


def fill_rect(canvas, x, y, w, h, color):
    fill_polygon(canvas, [(x, y), (x + w, y), (x + w, y + h), (x, y + h)], color)


def sword(canvas, fg, bg):
    fill_rect(canvas, 23, 8, 4, 22, fg)
    fill_rect(canvas, 16, 29, 18, 3, fg)
    fill_rect(canvas, 22, 32, 6, 9, fg)
    fill_circle(canvas, 25, 44, 3, fg)


def shield(canvas, fg, bg):
    fill_polygon(canvas, [(25, 6), (42, 13), (42, 27), (25, 44), (8, 27), (8, 13)], fg)


def helmet(canvas, fg, bg):
    fill_polygon(canvas, [(12, 36), (12, 24), (17, 12), (25, 8), (33, 12), (38, 24), (38, 36)], fg)
    fill_rect(canvas, 12, 25, 26, 5, bg)
    fill_rect(canvas, 23, 30, 4, 13, fg)


def spear(canvas, fg, bg):
    fill_polygon(canvas, [(25, 4), (31, 15), (25, 21), (19, 15)], fg)
    fill_rect(canvas, 23, 20, 4, 25, fg)


def crown(canvas, fg, bg):
    fill_polygon(canvas, [(8, 36), (8, 16), (16, 26), (25, 12), (34, 26), (42, 16), (42, 36)], fg)
    fill_rect(canvas, 8, 36, 34, 5, fg)


def heart(canvas, fg, bg):
    fill_circle(canvas, 18, 20, 9, fg)
    fill_circle(canvas, 32, 20, 9, fg)
    fill_polygon(canvas, [(9, 24), (41, 24), (25, 44)], fg)


def ring(canvas, fg, bg):
    fill_circle(canvas, 25, 25, 15, fg)
    fill_circle(canvas, 25, 25, 9, bg)


def bolt(canvas, fg, bg):
    fill_polygon(canvas, [(28, 4), (12, 27), (23, 27), (20, 46), (38, 21), (27, 21)], fg)


def hammer(canvas, fg, bg):
    fill_rect(canvas, 22, 14, 6, 30, fg)
    fill_rect(canvas, 10, 8, 30, 12, fg)


def star(canvas, fg, bg):
    fill_polygon(canvas, [
        (25, 5), (30, 19), (45, 19), (33, 28), (38, 43),
        (25, 34), (12, 43), (17, 28), (5, 19), (20, 19),
    ], fg)


SHAPES = [sword, shield, helmet, spear, crown, heart, ring, bolt, hammer, star]


def downsample(canvas):
    """Box-filter the supersampled canvas down to SIZE x SIZE."""
    out = bytearray()
    block = SCALE * SCALE
    for y in range(SIZE):
        out.append(0)  # PNG filter type 0 (None) for this scanline
        for x in range(SIZE):
            r = g = b = 0
            for dy in range(SCALE):
                row = canvas[y * SCALE + dy]
                for dx in range(SCALE):
                    pixel = row[x * SCALE + dx]
                    r += pixel[0]
                    g += pixel[1]
                    b += pixel[2]
            out.append(r // block)
            out.append(g // block)
            out.append(b // block)
    return bytes(out)


def write_png(path, raw_rows):
    def chunk(tag, data):
        body = tag + data
        return struct.pack('>I', len(data)) + body + struct.pack('>I', zlib.crc32(body) & 0xFFFFFFFF)

    header = struct.pack('>IIBBBBB', SIZE, SIZE, 8, 2, 0, 0, 0)  # 8-bit truecolor
    png = b'\x89PNG\r\n\x1a\n'
    png += chunk(b'IHDR', header)
    png += chunk(b'IDAT', zlib.compress(raw_rows, 9))
    png += chunk(b'IEND', b'')
    path.write_bytes(png)


def main():
    for index, (background, foreground) in enumerate(PALETTE):
        canvas = new_canvas(background)
        SHAPES[index](canvas, foreground, background)
        path = OUT_DIR / f'avatar_{index + 1:02d}.png'
        write_png(path, downsample(canvas))
        print(f'wrote {path.name}')


if __name__ == '__main__':
    main()
```

Run it and verify the output:

```bash
cd /Users/feor/projects/students/TOPAI/Practice/knights_fight
python3 kf.api/Assets/avatars/generate_avatars.py
python3 - <<'PY'
import struct, pathlib
for i in range(1, 11):
    p = pathlib.Path(f'kf.api/Assets/avatars/avatar_{i:02d}.png')
    d = p.read_bytes()
    assert d[:8] == b'\x89PNG\r\n\x1a\n', p
    w, h = struct.unpack('>II', d[16:24])
    assert (w, h) == (50, 50), (p, w, h)
    print(f'{p.name}: {w}x{h} {len(d)}B')
PY
```

Expected: ten lines, each `50x50`, no assertion errors.

- [ ] **Step 2: Write the failing provider test**

Create `kf.api.Tests/Infrastructure/Services/DefaultAvatarProviderTests.cs`:

```csharp
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
```

- [ ] **Step 3: Run the test to verify it fails**

```bash
cd /Users/feor/projects/students/TOPAI/Practice/knights_fight
dotnet test kf.api.Tests --filter "FullyQualifiedName~DefaultAvatarProviderTests"
```

Expected: FAIL — `KnightFight.Api.Infrastructure.Services.IDefaultAvatarProvider` does not exist.

- [ ] **Step 4: Write the provider**

Create `kf.api/Infrastructure/Services/DefaultAvatarProvider.cs`:

```csharp
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
```

- [ ] **Step 5: Embed the assets and register the provider**

In `kf.api/kf.api.csproj`, add the package reference and the embedded resources:

```xml
<ItemGroup>
  <PackageReference Include="SkiaSharp" Version="4.155.0" />
</ItemGroup>

<ItemGroup>
  <EmbeddedResource Include="Assets/avatars/avatar_*.png">
    <LogicalName>KnightFight.Api.Assets.avatars.%(Filename)%(Extension)</LogicalName>
  </EmbeddedResource>
</ItemGroup>
```

In `kf.api/Program.cs`, add the registration next to the other service registrations:

```csharp
builder.Services.AddSingleton<IDefaultAvatarProvider, DefaultAvatarProvider>();
```

- [ ] **Step 6: Run the test to verify it passes**

```bash
cd /Users/feor/projects/students/TOPAI/Practice/knights_fight
dotnet test kf.api.Tests --filter "FullyQualifiedName~DefaultAvatarProviderTests"
```

Expected: PASS — 4 tests.

- [ ] **Step 7: Commit**

```bash
cd /Users/feor/projects/students/TOPAI/Practice/knights_fight
git add kf.api/Assets/avatars kf.api/Infrastructure/Services/DefaultAvatarProvider.cs \
        kf.api/kf.api.csproj kf.api/Program.cs \
        kf.api.Tests/Infrastructure/Services/DefaultAvatarProviderTests.cs
git commit -m "feat: add ten default challenger avatars and their provider"
```

---

### Task 2: Avatar image normalization service

**Files:**
- Create: `kf.api/Infrastructure/Services/Images/IImageService.cs`
- Create: `kf.api/Infrastructure/Services/Images/AvatarImageService.cs`
- Test: `kf.api.Tests/Infrastructure/Services/AvatarImageServiceTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces:
  ```csharp
  namespace KnightFight.Api.Infrastructure.Services.Images;

  public enum AvatarImageError { None, UnsupportedFormat, TooLarge, DecodeFailed, Empty }

  public record AvatarImageResult(bool IsSuccess, byte[]? Bytes, AvatarImageError Error)
  {
      public static AvatarImageResult Success(byte[] bytes);
      public static AvatarImageResult Failure(AvatarImageError error);
  }

  public interface IImageService
  {
      Task<AvatarImageResult> NormalizeAsync(Stream input, CancellationToken cancellationToken = default);
  }

  public class AvatarImageService : IImageService
  {
      public const int AvatarSize = 50;
      public const int MaxDimension = 4096;
  }
  ```

- [ ] **Step 1: Write the failing test**

Create `kf.api.Tests/Infrastructure/Services/AvatarImageServiceTests.cs`:

```csharp
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
    public async Task NormalizeAsync_Png_ProducesFiftyByFiftyPng()
    {
        var source = MakeImage(120, 80, SKEncodedImageFormat.Png);

        var result = await _service.NormalizeAsync(new MemoryStream(source));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Bytes);
        Assert.Equal(AvatarImageError.None, result.Error);

        var (width, height) = SizeOf(result.Bytes!);
        Assert.Equal(50, width);
        Assert.Equal(50, height);
    }

    [Fact]
    public async Task NormalizeAsync_Jpeg_ProducesFiftyByFiftyPng()
    {
        var source = MakeImage(64, 64, SKEncodedImageFormat.Jpeg);

        var result = await _service.NormalizeAsync(new MemoryStream(source));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Bytes);

        // re-encoded as PNG regardless of the source format
        var (width, height) = SizeOf(result.Bytes!);
        Assert.Equal(50, width);
        Assert.Equal(50, height);
        Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, result.Bytes!.Take(4).ToArray());
    }

    [Fact]
    public async Task NormalizeAsync_TextFile_IsRejected()
    {
        var source = System.Text.Encoding.UTF8.GetBytes("this is not an image");

        var result = await _service.NormalizeAsync(new MemoryStream(source));

        Assert.False(result.IsSuccess);
        Assert.Equal(AvatarImageError.UnsupportedFormat, result.Error);
        Assert.Null(result.Bytes);
    }

    [Fact]
    public async Task NormalizeAsync_EmptyStream_IsRejected()
    {
        var result = await _service.NormalizeAsync(new MemoryStream());

        Assert.False(result.IsSuccess);
        Assert.Equal(AvatarImageError.Empty, result.Error);
    }

    [Fact]
    public async Task NormalizeAsync_OversizedImage_IsRejected()
    {
        // a 5000x10 image is cheap to make but exceeds the dimension guard
        var source = MakeImage(5000, 10, SKEncodedImageFormat.Png);

        var result = await _service.NormalizeAsync(new MemoryStream(source));

        Assert.False(result.IsSuccess);
        Assert.Equal(AvatarImageError.TooLarge, result.Error);
    }

    [Fact]
    public async Task NormalizeAsync_NonSquareImage_CropsToSquare()
    {
        var source = MakeImage(200, 100, SKEncodedImageFormat.Png);

        var result = await _service.NormalizeAsync(new MemoryStream(source));

        Assert.True(result.IsSuccess);
        var (width, height) = SizeOf(result.Bytes!);
        Assert.Equal(50, width);
        Assert.Equal(50, height);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

```bash
cd /Users/feor/projects/students/TOPAI/Practice/knights_fight
dotnet test kf.api.Tests --filter "FullyQualifiedName~AvatarImageServiceTests"
```

Expected: FAIL — `KnightFight.Api.Infrastructure.Services.Images` does not exist.

- [ ] **Step 3: Write the interface and result type**

Create `kf.api/Infrastructure/Services/Images/IImageService.cs`:

```csharp
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
```

- [ ] **Step 4: Write the implementation**

Create `kf.api/Infrastructure/Services/Images/AvatarImageService.cs`:

```csharp
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
            using var paint = new SKPaint { IsAntialias = true };
            canvas.DrawBitmap(
                bitmap,
                source,
                target,
                new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear),
                paint);
        }

        using var image = SKImage.FromBitmap(output);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);

        return data?.ToArray();
    }
}
```

> **SkiaSharp 4.x note:** `SKPaint.FilterQuality` and the `SKFilterQuality` enum no longer exist — the sampling mode is passed as an `SKSamplingOptions` argument to `DrawBitmap`. Using the old API is a compile error.

- [ ] **Step 5: Register the service**

In `kf.api/Program.cs`:

```csharp
builder.Services.AddSingleton<IImageService, AvatarImageService>();
```

- [ ] **Step 6: Run the test to verify it passes**

```bash
cd /Users/feor/projects/students/TOPAI/Practice/knights_fight
dotnet test kf.api.Tests --filter "FullyQualifiedName~AvatarImageServiceTests"
```

Expected: PASS — 6 tests.

- [ ] **Step 7: Commit**

```bash
cd /Users/feor/projects/students/TOPAI/Practice/knights_fight
git add kf.api/Infrastructure/Services/Images kf.api/Program.cs \
        kf.api.Tests/Infrastructure/Services/AvatarImageServiceTests.cs
git commit -m "feat: add avatar image normalization service"
```

---

### Task 3: Entity columns and EF migration

**Files:**
- Modify: `kf.api/Models/Entities/ChallengerModel.cs`
- Modify: `kf.api/Infrastructure/KfContext.cs`
- Create: `kf.api/Migrations/<timestamp>_AddChallengerAvatar.cs` (generated)
- Modify: `kf.api/Migrations/KfContextModelSnapshot.cs` (regenerated)

**Interfaces:**
- Consumes: nothing.
- Produces: on `ChallengerModel` —
  ```csharp
  public byte[]? AvatarBlob { get; set; }
  public int? DefaultAvatarIndex { get; set; }
  ```

- [ ] **Step 1: Add the columns to the entity**

In `kf.api/Models/Entities/ChallengerModel.cs`, add before the closing brace:

```csharp
    /// <summary>
    /// The challenger's own avatar, already resized to 50x50 and stored as PNG.
    /// Null when the challenger uses one of the bundled default avatars.
    /// </summary>
    public byte[]? AvatarBlob { get; set; }
    
    /// <summary>
    /// Index into the ten bundled default avatars (0..9).
    /// Null when the challenger has a custom <see cref="AvatarBlob"/>.
    /// </summary>
    public int? DefaultAvatarIndex { get; set; }
```

- [ ] **Step 2: Declare the mutual-exclusion constraint in the model**

A challenger must have exactly one avatar source — a custom blob **or** a default index, never both and never neither. Declare the constraint in `KfContext.OnModelCreating` so EF owns it and future migrations keep it in sync.

In `kf.api/Infrastructure/KfContext.cs`, add at the start of `OnModelCreating`:

```csharp
        modelBuilder.Entity<ChallengerModel>()
            .ToTable("Challengers", table => table.HasCheckConstraint(
                "CK_Challengers_Avatar_SingleSource",
                "(\"AvatarBlob\" IS NOT NULL AND \"DefaultAvatarIndex\" IS NULL) OR (\"AvatarBlob\" IS NULL AND \"DefaultAvatarIndex\" IS NOT NULL)"));
```

The in-memory database the contract tests use does not enforce CHECK constraints, but the model still builds and every test passes with this declaration — verified by running `dotnet test kf.api.Tests` after this step.

- [ ] **Step 3: Make sure the database is up and the password is in the environment**

```bash
cd /Users/feor/projects/students/TOPAI/Practice/knights_fight
docker compose -f dev.compose.yaml up -d
echo "POSTGRES_PW is ${POSTGRES_PW:+set}"
```

If the second line prints `POSTGRES_PW is ` (empty), export it in the shell before continuing — `dotnet ef` builds the API host, and the host applies migrations on startup, so it needs a reachable database and a valid password. **Do not read or echo `.env`.**

- [ ] **Step 4: Generate the migration**

```bash
cd /Users/feor/projects/students/TOPAI/Practice/knights_fight
dotnet ef migrations add AddChallengerAvatar --project kf.api --startup-project kf.api
```

Expected: a new file `kf.api/Migrations/<timestamp>_AddChallengerAvatar.cs` and an updated `KfContextModelSnapshot.cs`. The migration contains two `AddColumn` calls and one `AddCheckConstraint` — the constraint comes from `OnModelCreating`, so **do not hand-edit the migration**.

- [ ] **Step 5: Verify the migration does what the spec asks**

```bash
cd /Users/feor/projects/students/TOPAI/Practice/knights_fight
grep -n "AvatarBlob\|DefaultAvatarIndex\|AddCheckConstraint" kf.api/Migrations/*_AddChallengerAvatar.cs
grep -n "AvatarBlob\|DefaultAvatarIndex" kf.api/Migrations/KfContextModelSnapshot.cs
```

Expected: `AvatarBlob` as a nullable `bytea` column, `DefaultAvatarIndex` as a nullable `integer` column, an `AddCheckConstraint` call, and both properties present in the snapshot.

- [ ] **Step 6: Apply the migration to the dev database**

```bash
cd /Users/feor/projects/students/TOPAI/Practice/knights_fight
dotnet ef database update --project kf.api --startup-project kf.api
```

Expected: `Done.`

- [ ] **Step 7: Build and commit**

```bash
cd /Users/feor/projects/students/TOPAI/Practice/knights_fight
dotnet build kf.api/kf.api.csproj
dotnet test kf.api.Tests
git add kf.api/Models/Entities/ChallengerModel.cs kf.api/Infrastructure/KfContext.cs kf.api/Migrations
git commit -m "feat: add avatar columns to the challengers table"
```

Expected: the build succeeds with no errors.

---

### Task 4: Avatar URL helper and the GET avatar endpoint

**Files:**
- Create: `kf.api/Models/Mappers/AvatarUrl.cs`
- Create: `kf.api/Features/Challenger/ChallengerAvatarGet.cs`
- Modify: `kf.api/Infrastructure/ApiMap.cs`
- Test: `kf.api.Tests/AvatarContractTests.cs`

**Interfaces:**
- Consumes: `IDefaultAvatarProvider` (Task 1), `ChallengerModel.AvatarBlob` / `DefaultAvatarIndex` (Task 3).
- Produces:
  ```csharp
  namespace KnightFight.Api.Models.Mappers;

  public static class AvatarUrl
  {
      public static string For(long challengerId);
  }
  ```
  and the route `GET api/challengers/{id}/avatar` (anonymous), returning `image/png`.

- [ ] **Step 1: Write the failing contract test**

Create `kf.api.Tests/AvatarContractTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using KnightFight.Api.Infrastructure;
using KnightFight.Api.Models.Entities;
using KnightFight.Api.Models.Responses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KnightFight.Api.Tests;

public class AvatarContractTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public AvatarContractTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetAvatar_UnknownChallenger_ReturnsNotFound()
    {
        var response = await _client.GetAsync("/api/challengers/999999/avatar");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetAvatar_ChallengerWithCustomBlob_ReturnsPng()
    {
        var png = MakePng();
        var id = await SeedChallenger("AvatarCustom", c =>
        {
            c.AvatarBlob = png;
            c.DefaultAvatarIndex = null;
        });

        var response = await _client.GetAsync($"/api/challengers/{id}/avatar");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/png", response.Content.Headers.ContentType?.MediaType);

        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.Equal(png, bytes);
    }

    [Fact]
    public async Task GetAvatar_ChallengerWithDefault_ReturnsTheBundledAvatar()
    {
        var id = await SeedChallenger("AvatarDefault", c =>
        {
            c.AvatarBlob = null;
            c.DefaultAvatarIndex = 3;
        });

        var response = await _client.GetAsync($"/api/challengers/{id}/avatar");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/png", response.Content.Headers.ContentType?.MediaType);

        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, bytes.Take(4).ToArray());
    }

    [Fact]
    public async Task GetAvatar_SetsAnEtag()
    {
        var id = await SeedChallenger("AvatarEtag", c =>
        {
            c.AvatarBlob = MakePng();
            c.DefaultAvatarIndex = null;
        });

        var response = await _client.GetAsync($"/api/challengers/{id}/avatar");

        Assert.NotNull(response.Headers.ETag);
        Assert.NotEmpty(response.Headers.ETag!.Tag);
    }

    private async Task<long> SeedChallenger(string name, Action<ChallengerModel> configure)
    {
        await _factory.SeedDatabaseAsync(ctx =>
        {
            if (ctx.Challengers.Any(c => c.ChallengerName == name)) return;

            var entity = new ChallengerModel
            {
                ChallengerName = name,
                Secret = "hash"
            };
            configure(entity);
            ctx.Challengers.Add(entity);
        });

        using var scope = _factory.Services.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<KfContext>>();
        await using var ctx = await factory.CreateDbContextAsync();
        var seeded = await ctx.Challengers.SingleAsync(c => c.ChallengerName == name);
        return seeded.Id;
    }

    private static byte[] MakePng()
    {
        using var bitmap = new SkiaSharp.SKBitmap(50, 50);
        using (var canvas = new SkiaSharp.SKCanvas(bitmap))
        {
            canvas.Clear(SkiaSharp.SKColors.DodgerBlue);
        }

        using var data = bitmap.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

```bash
cd /Users/feor/projects/students/TOPAI/Practice/knights_fight
dotnet test kf.api.Tests --filter "FullyQualifiedName~AvatarContractTests"
```

Expected: FAIL — the route `api/challengers/{id}/avatar` does not exist (404).

- [ ] **Step 3: Add the route constant**

In `kf.api/Infrastructure/ApiMap.cs`, inside `public static class Challenger`, add:

```csharp
        public const string AVATAR = $"{API_ROOT}/challengers/{{id}}/avatar";
```

The value is `api/challengers/{id}/avatar` — `{{` and `}}` are the escaped braces of an interpolated string.

- [ ] **Step 4: Write the URL helper**

Create `kf.api/Models/Mappers/AvatarUrl.cs`:

```csharp
using System.Globalization;
using KnightFight.Api.Infrastructure;

namespace KnightFight.Api.Models.Mappers;

/// <summary>
/// Builds the URL a client uses to fetch a challenger's avatar.
/// </summary>
/// <remarks>
/// The URL is root-relative on purpose: in development the Vite proxy forwards
/// /api to the API, in production nginx does the same. A path-relative URL
/// would break on any SPA route other than "/".
/// </remarks>
public static class AvatarUrl
{
    public static string For(long challengerId)
        => "/" + ApiMap.Challenger.AVATAR.Replace(
            "{id}",
            challengerId.ToString(CultureInfo.InvariantCulture),
            StringComparison.Ordinal);
}
```

- [ ] **Step 5: Write the GET endpoint**

Create `kf.api/Features/Challenger/ChallengerAvatarGet.cs`:

```csharp
using System.Security.Cryptography;
using KnightFight.Api.Infrastructure;
using KnightFight.Api.Infrastructure.ApiMethods;
using KnightFight.Api.Infrastructure.Services;
using KnightFight.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace KnightFight.Api.Features.Challenger;

/// <summary>
/// Serves a challenger's avatar as PNG.
/// </summary>
/// <remarks>
/// The route is anonymous: the avatar is public data and the leaderboard
/// renders it without a login. A challenger that was given a custom avatar
/// serves the stored blob; one that was not serves the bundled default its
/// row points at.
/// </remarks>
public class ChallengerAvatarGet(
    IDbContextFactory<KfContext> factory,
    IDefaultAvatarProvider defaultAvatars)
    : ApiMethod(ApiMap.Challenger.AVATAR)
{
    private const string CacheControl = "public, max-age=300";

    public override void MapEndpoint(WebApplication app)
    {
        app.MapGet(UriPattern, Handler);
    }

    public async Task<IResult> Handler(long id, HttpContext http)
    {
        await using var ctx = await factory.CreateDbContextAsync();

        var challenger = await ctx.Challengers
            .AsNoTracking()
            .SingleOrDefaultAsync(c => c.Id == id);

        if (challenger is null)
            return Results.NotFound();

        var bytes = ResolveBytes(challenger);
        if (bytes is null)
            return Results.NotFound();

        var etag = $"\"{Convert.ToHexString(SHA256.HashData(bytes))[..16]}\"";

        http.Response.Headers.ETag = etag;
        http.Response.Headers.CacheControl = CacheControl;

        var ifNoneMatch = http.Request.Headers.IfNoneMatch;
        if (ifNoneMatch.Count > 0 &&
            ifNoneMatch.Any(value => string.Equals(value, etag, StringComparison.Ordinal)))
            return Results.StatusCode(StatusCodes.Status304NotModified);

        return Results.File(bytes, "image/png");
    }

    private byte[]? ResolveBytes(ChallengerModel challenger)
    {
        if (challenger.AvatarBlob is not null)
            return challenger.AvatarBlob;

        if (challenger.DefaultAvatarIndex is not int index)
            return null;

        // an index outside the bundled set means the asset was removed - serve nothing
        if (index < 0 || index >= defaultAvatars.Avatars.Count)
            return null;

        return defaultAvatars.GetBytes(index);
    }
}
```

- [ ] **Step 6: Run the test to verify it passes**

```bash
cd /Users/feor/projects/students/TOPAI/Practice/knights_fight
dotnet test kf.api.Tests --filter "FullyQualifiedName~AvatarContractTests"
```

Expected: PASS — 4 tests.

- [ ] **Step 7: Commit**

```bash
cd /Users/feor/projects/students/TOPAI/Practice/knights_fight
git add kf.api/Infrastructure/ApiMap.cs kf.api/Models/Mappers/AvatarUrl.cs \
        kf.api/Features/Challenger/ChallengerAvatarGet.cs kf.api.Tests/AvatarContractTests.cs
git commit -m "feat: serve challenger avatars over GET api/challengers/{id}/avatar"
```

---

### Task 5: Default avatar assignment at creation

**Files:**
- Modify: `kf.api/Models/Mappers/ChallengerMapper.cs`
- Modify: `kf.api/Features/Challenger/ChallengerCreate.cs`
- Test: `kf.api.Tests/AvatarContractTests.cs`

**Interfaces:**
- Consumes: `IDefaultAvatarProvider.RandomIndex()` (Task 1).
- Produces: `ChallengerMapper.ToEntity(this CreateChallengerRequest source, string secretHash, int defaultAvatarIndex)`.

- [ ] **Step 1: Write the failing contract test**

Add to `kf.api.Tests/AvatarContractTests.cs`:

```csharp
    [Fact]
    public async Task CreateChallenger_AssignsARandomDefaultAvatar()
    {
        var name = $"CreatedWithDefault{Guid.NewGuid():N}";

        var response = await EncryptedRequestFactory.PostEncryptedAsync(
            _client,
            "/api/challenger",
            JsonSerializer.Serialize(new { ChallengerName = name, Secret = "s3cret" }));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var id = body.GetProperty("resultValue").GetProperty("id").GetInt64();

        using var scope = _factory.Services.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<KfContext>>();
        await using var ctx = await factory.CreateDbContextAsync();
        var challenger = await ctx.Challengers.SingleAsync(c => c.Id == id);

        Assert.Null(challenger.AvatarBlob);
        Assert.NotNull(challenger.DefaultAvatarIndex);
        Assert.InRange(challenger.DefaultAvatarIndex!.Value, 0, 9);

        // the assigned default is immediately fetchable
        var avatar = await _client.GetAsync($"/api/challengers/{id}/avatar");
        Assert.Equal(HttpStatusCode.OK, avatar.StatusCode);
        Assert.Equal("image/png", avatar.Content.Headers.ContentType?.MediaType);
    }
```

- [ ] **Step 2: Write the encrypted-request test helper**

Create `kf.api.Tests/Infrastructure/EncryptedRequestFactory.cs`:

```csharp
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace KnightFight.Api.Tests.Infrastructure;

/// <summary>
/// Posts an RSA-encrypted body the way kf.client does, so contract tests can
/// drive the encrypted endpoints.
/// </summary>
/// <remarks>
/// The inner payload is deserialized with default JsonSerializerOptions, so
/// its property names must be PascalCase: {"ChallengerName":"...","Secret":"..."}.
/// </remarks>
public static class EncryptedRequestFactory
{
    public static async Task<HttpResponseMessage> PostEncryptedAsync(
        HttpClient client,
        string url,
        string innerJson)
    {
        var pemResponse = await client.GetAsync("/api/Auth/public-key");
        pemResponse.EnsureSuccessStatusCode();

        var pem = JsonSerializer.Deserialize<string>(await pemResponse.Content.ReadAsStringAsync())!;

        using var rsa = RSA.Create();
        rsa.ImportFromPem(pem);

        var cipher = rsa.Encrypt(Encoding.UTF8.GetBytes(innerJson), RSAEncryptionPadding.OaepSHA256);
        var envelope = JsonSerializer.Serialize(new { Payload = Convert.ToBase64String(cipher) });

        return await client.PostAsync(url, new StringContent(envelope, Encoding.UTF8, "application/json"));
    }
}
```

Add the using to the top of `kf.api.Tests/AvatarContractTests.cs`:

```csharp
using KnightFight.Api.Tests.Infrastructure;
```

- [ ] **Step 3: Run the test to verify it fails**

```bash
cd /Users/feor/projects/students/TOPAI/Practice/knights_fight
dotnet test kf.api.Tests --filter "FullyQualifiedName~AvatarContractTests"
```

Expected: FAIL — `DefaultAvatarIndex` is null for a newly created challenger.

- [ ] **Step 4: Change the mapper**

In `kf.api/Models/Mappers/ChallengerMapper.cs`, replace `ToEntity` with:

```csharp
    public static ChallengerModel ToEntity(this CreateChallengerRequest source, string secretHash, int defaultAvatarIndex)
        => new()
        {
            //Id = 0,
            // CreatedAt = default,
            // UpdatedAt = default,
            ChallengerName = source.ChallengerName,
            Secret = secretHash,
            DefaultAvatarIndex = defaultAvatarIndex,
            AvatarBlob = null,
            // Challenges = 0,
            // Wins = 0,
            // Loses = 0,
            // Draws = 0
        };
```

- [ ] **Step 5: Assign the default in the create endpoint**

In `kf.api/Features/Challenger/ChallengerCreate.cs`, add `IDefaultAvatarProvider defaultAvatars` to the primary constructor and pass the random index:

```csharp
public class ChallengerCreate(
    IDbContextFactory<KfContext> factory,
    ILogger<ChallengerCreate> logger,
    INewcomerNotificationService notificationService,
    IDefaultAvatarProvider defaultAvatars)
    : EncryptedPostEndpoint<CreateChallengerRequest>(ApiMap.Challenger.CREATE)
```

and inside `CreateNewChallenger`:

```csharp
        var en = ctx.Challengers.Add(
            request.ToEntity(
                await CryptoService.GetHash(request.Secret),
                defaultAvatars.RandomIndex()
            )
        );
```

- [ ] **Step 6: Run the test to verify it passes**

```bash
cd /Users/feor/projects/students/TOPAI/Practice/knights_fight
dotnet test kf.api.Tests --filter "FullyQualifiedName~AvatarContractTests"
```

Expected: PASS — 5 tests.

- [ ] **Step 7: Commit**

```bash
cd /Users/feor/projects/students/TOPAI/Practice/knights_fight
git add kf.api/Models/Mappers/ChallengerMapper.cs kf.api/Features/Challenger/ChallengerCreate.cs \
        kf.api.Tests/AvatarContractTests.cs kf.api.Tests/Infrastructure/EncryptedRequestFactory.cs
git commit -m "feat: assign a random default avatar when a challenger is created"
```

---

### Task 6: Avatar URLs in the read responses

**Files:**
- Modify: `kf.api/Features/Leaderboard/LeaderboardGet.cs`
- Modify: `kf.api/Features/Challenger/ChallengerGetAll.cs`
- Modify: `kf.api/Features/Challenger/ChallengerCreate.cs` (the newcomer event)
- Test: `kf.api.Tests/AvatarContractTests.cs`

**Interfaces:**
- Consumes: `AvatarUrl.For(long)` (Task 4).
- Produces: `RankedChallengerResponse.AvatarUrl`, `ChallengerItemResponse.AvatarUrl` and `ChallengerNewcomerEvent.AvatarUrl` all carry `/api/challengers/{id}/avatar`.

- [ ] **Step 1: Write the failing contract test**

Add to `kf.api.Tests/AvatarContractTests.cs`:

```csharp
    [Fact]
    public async Task GetLeaderboard_IncludesTheAvatarUrl()
    {
        var id = await SeedChallenger("LeaderboardAvatar", c =>
        {
            c.AvatarBlob = MakePng();
            c.DefaultAvatarIndex = null;
            c.Wins = 5;
        });

        var response = await _client.GetAsync("/api/leaderboard?limit=100");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var items = await response.Content.ReadFromJsonAsync<List<RankedChallengerResponse>>(JsonOptions);
        Assert.NotNull(items);

        var entry = items!.First(i => i.Id == id);
        Assert.Equal($"/api/challengers/{id}/avatar", entry.AvatarUrl);
    }

    [Fact]
    public async Task GetChallengers_IncludesTheAvatarUrl()
    {
        var id = await SeedChallenger("PagedAvatar", c =>
        {
            c.AvatarBlob = MakePng();
            c.DefaultAvatarIndex = null;
        });

        var response = await _client.GetAsync("/api/challengers?page=0&page_size=100");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var page = await response.Content.ReadFromJsonAsync<PagedChallengersResponse>(JsonOptions);
        Assert.NotNull(page);

        var entry = page!.Items.First(i => i.Id == id);
        Assert.Equal($"/api/challengers/{id}/avatar", entry.AvatarUrl);
    }
```

- [ ] **Step 2: Run the test to verify it fails**

```bash
cd /Users/feor/projects/students/TOPAI/Practice/knights_fight
dotnet test kf.api.Tests --filter "FullyQualifiedName~AvatarContractTests"
```

Expected: FAIL — `avatarUrl` is null.

- [ ] **Step 3: Fill the leaderboard response**

In `kf.api/Features/Leaderboard/LeaderboardGet.cs`, add the using and change the last argument of the `RankedChallengerResponse` constructor from `null` to `AvatarUrl.For(c.Id)`:

```csharp
using KnightFight.Api.Models.Mappers;
```

```csharp
        var result = rankedEntities
            .Select((c, index) => new RankedChallengerResponse(
                c.Id,
                c.ChallengerName,
                index + 1,
                c.Wins,
                c.Loses,
                c.Score,
                AvatarUrl.For(c.Id)
            ))
            .ToList();
```

- [ ] **Step 4: Fill the newcomers list response**

In `kf.api/Features/Challenger/ChallengerGetAll.cs`, add `using KnightFight.Api.Models.Mappers;` and change the projection:

```csharp
            .Select(c => new ChallengerItemResponse(c.Id, c.ChallengerName, c.CreatedAt, AvatarUrl.For(c.Id)))
```

- [ ] **Step 5: Fill the newcomer event**

In `kf.api/Features/Challenger/ChallengerCreate.cs`, add `using KnightFight.Api.Models.Mappers;` and change the published event:

```csharp
            await notificationService.PublishAsync(new ChallengerNewcomerEvent(
                en.Entity.Id,
                en.Entity.ChallengerName,
                en.Entity.CreatedAt,
                AvatarUrl.For(en.Entity.Id)));
```

- [ ] **Step 6: Run the test to verify it passes**

```bash
cd /Users/feor/projects/students/TOPAI/Practice/knights_fight
dotnet test kf.api.Tests --filter "FullyQualifiedName~AvatarContractTests"
```

Expected: PASS — 7 tests.

- [ ] **Step 7: Commit**

```bash
cd /Users/feor/projects/students/TOPAI/Practice/knights_fight
git add kf.api/Features/Leaderboard/LeaderboardGet.cs kf.api/Features/Challenger/ChallengerGetAll.cs \
        kf.api/Features/Challenger/ChallengerCreate.cs kf.api.Tests/AvatarContractTests.cs
git commit -m "feat: expose the avatar url on the leaderboard, newcomers and stream events"
```

---

### Task 7: Avatar upload endpoint

**Files:**
- Create: `kf.api/Features/Challenger/ChallengerAvatarUpload.cs`
- Test: `kf.api.Tests/AvatarContractTests.cs`

**Interfaces:**
- Consumes: `IImageService.NormalizeAsync` (Task 2), `ChallengerModel.AvatarBlob` (Task 3), `IAuthService` (existing).
- Produces: the route `POST api/challengers/{id}/avatar`, `[Authorize]`, multipart form field `avatar`, max 5 MB.

- [ ] **Step 1: Write the failing contract tests**

Add to `kf.api.Tests/AvatarContractTests.cs`:

```csharp
    [Fact]
    public async Task UploadAvatar_WithoutAToken_IsUnauthorized()
    {
        var id = await SeedChallenger("UploadNoToken", c => c.DefaultAvatarIndex = 1);

        using var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent(MakePng()), "avatar", "avatar.png");

        var response = await _client.PostAsync($"/api/challengers/{id}/avatar", content);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UploadAvatar_WithAForeignToken_IsForbidden()
    {
        var ownerId = await SeedChallenger("UploadOwner", c => c.DefaultAvatarIndex = 1);
        var otherId = await SeedChallenger("UploadOther", c => c.DefaultAvatarIndex = 2);

        var token = await TokenFor("UploadOwner");

        using var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent(MakePng()), "avatar", "avatar.png");

        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/challengers/{otherId}/avatar");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = content;

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UploadAvatar_WithATextFile_IsBadRequest()
    {
        var name = $"UploadText{Guid.NewGuid():N}";
        var id = await SeedChallenger(name, c => c.DefaultAvatarIndex = 1);
        var token = await TokenFor(name);

        using var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent("not an image"u8.ToArray()), "avatar", "avatar.txt");

        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/challengers/{id}/avatar");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = content;

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UploadAvatar_WithAPng_StoresItAndClearsTheDefault()
    {
        var name = $"UploadOk{Guid.NewGuid():N}";
        var id = await SeedChallenger(name, c => c.DefaultAvatarIndex = 4);
        var token = await TokenFor(name);

        using var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent(MakePng()), "avatar", "avatar.png");

        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/challengers/{id}/avatar");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = content;

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<KfContext>>();
        await using var ctx = await factory.CreateDbContextAsync();
        var challenger = await ctx.Challengers.SingleAsync(c => c.Id == id);

        Assert.NotNull(challenger.AvatarBlob);
        Assert.Null(challenger.DefaultAvatarIndex);

        // and it is what the avatar route now serves
        var served = await _client.GetAsync($"/api/challengers/{id}/avatar");
        Assert.Equal(HttpStatusCode.OK, served.StatusCode);
    }

    private async Task<string> TokenFor(string challengerName)
    {
        using var scope = _factory.Services.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<KfContext>>();
        await using var ctx = await factory.CreateDbContextAsync();
        var challenger = await ctx.Challengers.SingleAsync(c => c.ChallengerName == challengerName);

        return _factory.Services.GetRequiredService<IAuthService>().GenerateToken(challenger);
    }
```

Add the using for `IAuthService`:

```csharp
using KnightFight.Api.Infrastructure.Services;
```

- [ ] **Step 2: Run the tests to verify they fail**

```bash
cd /Users/feor/projects/students/TOPAI/Practice/knights_fight
dotnet test kf.api.Tests --filter "FullyQualifiedName~AvatarContractTests"
```

Expected: FAIL — the POST route does not exist (404 for every case).

- [ ] **Step 3: Write the upload endpoint**

Create `kf.api/Features/Challenger/ChallengerAvatarUpload.cs`:

```csharp
using System.Security.Claims;
using KnightFight.Api.Infrastructure;
using KnightFight.Api.Infrastructure.ApiMethods;
using KnightFight.Api.Infrastructure.Services.Images;
using KnightFight.Api.Models.Mappers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;

namespace KnightFight.Api.Features.Challenger;

/// <summary>
/// Accepts a challenger's avatar image and stores it.
/// </summary>
/// <remarks>
/// This is the API's second authorized route. The JWT already carries the
/// challenger id in its "sub" claim, so the caller is identified without any
/// extra credential: a token for another challenger is rejected.
/// </remarks>
public class ChallengerAvatarUpload(
    IDbContextFactory<KfContext> factory,
    IImageService imageService,
    ILogger<ChallengerAvatarUpload> logger)
    : ApiMethod(ApiMap.Challenger.AVATAR)
{
    public const long MaxUploadBytes = 5 * 1024 * 1024;

    private const string FileFieldName = "avatar";

    public override void MapEndpoint(WebApplication app)
    {
        // RequireAuthorization is explicit as well as the attribute, so the
        // requirement never depends on metadata inference.
        app.MapPost(UriPattern, Handler).RequireAuthorization();
    }

    [Authorize]
    [RequestSizeLimit(MaxUploadBytes)]
    public async Task<IResult> Handler(long id, HttpContext http, IFormFile? avatar)
    {
        var callerId = GetChallengerId(http.User);
        if (callerId is null)
            return Results.Unauthorized();

        if (callerId.Value != id)
            return Results.Forbid();

        if (avatar is null || avatar.Length == 0)
            return Results.BadRequest(new { message = $"A file part named '{FileFieldName}' is required." });

        if (avatar.Length > MaxUploadBytes)
            return Results.BadRequest(new { message = "The avatar is too large." });

        await using var upload = avatar.OpenReadStream();
        var normalized = await imageService.NormalizeAsync(upload, http.RequestAborted);

        if (!normalized.IsSuccess)
        {
            if (logger.IsEnabled(LogLevel.Warning))
                logger.LogWarning("Avatar upload rejected: {Error}", normalized.Error);

            return Results.BadRequest(new { message = normalized.Error.ToString() });
        }

        await using var ctx = await factory.CreateDbContextAsync();

        var challenger = await ctx.Challengers.SingleOrDefaultAsync(c => c.Id == id);
        if (challenger is null)
            return Results.NotFound();

        challenger.AvatarBlob = normalized.Bytes;
        challenger.DefaultAvatarIndex = null;

        await ctx.SaveChangesAsync(http.RequestAborted);

        return Results.Ok(new { id, avatarUrl = AvatarUrl.For(id) });
    }

    /// <summary>
    /// Reads the challenger id from the JWT. The "sub" claim is mapped to
    /// NameIdentifier by the default inbound claim mapping, so both are tried.
    /// </summary>
    private static long? GetChallengerId(ClaimsPrincipal user)
    {
        var raw = user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                  ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        return long.TryParse(raw, out var id) ? id : null;
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

```bash
cd /Users/feor/projects/students/TOPAI/Practice/knights_fight
dotnet test kf.api.Tests --filter "FullyQualifiedName~AvatarContractTests"
```

Expected: PASS — 11 tests.

- [ ] **Step 5: Run the whole API test suite**

```bash
cd /Users/feor/projects/students/TOPAI/Practice/knights_fight
dotnet test kf.api.Tests
```

Expected: PASS — no failures anywhere. The existing `EndpointContractTests` must still pass; the create endpoint's response shape did not change.

- [ ] **Step 6: Commit**

```bash
cd /Users/feor/projects/students/TOPAI/Practice/knights_fight
git add kf.api/Features/Challenger/ChallengerAvatarUpload.cs kf.api.Tests/AvatarContractTests.cs
git commit -m "feat: accept avatar uploads on POST api/challengers/{id}/avatar"
```

---

### Task 8: Web UI renders avatars

**Files:**
- Modify: `kf.web_ui/src/widgets/leaderboard/LeaderboardTable.tsx`
- Modify: `kf.web_ui/src/widgets/newcomers/NewcomersList.tsx`
- Create: `kf.web_ui/src/widgets/leaderboard/LeaderboardTable.test.tsx`

**Interfaces:**
- Consumes: `RankedChallenger.avatarUrl` and `Challenger.avatarUrl` (already declared in the entity models).
- Produces: no new exports; the two list components render an `<img>` when the URL is present.

- [ ] **Step 1: Write the failing test**

Create `kf.web_ui/src/widgets/leaderboard/LeaderboardTable.test.tsx`:

```tsx
import { act } from 'react';
import { createRoot } from 'react-dom/client';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import type { RankedChallenger } from '@/entities/ranked-challenger';

import { LeaderboardTable } from './LeaderboardTable';

declare global {
    // eslint-disable-next-line no-var
    var IS_REACT_ACT_ENVIRONMENT: boolean | undefined;
}

function challenger(overrides: Partial<RankedChallenger> = {}): RankedChallenger {
    return {
        id: 1,
        name: 'Alice',
        rank: 1,
        wins: 3,
        losses: 1,
        score: 9,
        ...overrides,
    };
}

async function renderTable(items: RankedChallenger[]) {
    const container = document.createElement('div');
    document.body.appendChild(container);

    const root = createRoot(container);

    await act(async () => {
        root.render(<LeaderboardTable items={items} />);
    });

    return container;
}

describe('LeaderboardTable', () => {
    beforeEach(() => {
        globalThis.IS_REACT_ACT_ENVIRONMENT = true;
    });

    afterEach(() => {
        document.body.innerHTML = '';
    });

    it('renders the avatar image when the challenger has one', async () => {
        const container = await renderTable([
            challenger({ avatarUrl: '/api/challengers/1/avatar' }),
        ]);

        const image = container.querySelector('img');
        expect(image).not.toBeNull();
        expect(image?.getAttribute('src')).toBe('/api/challengers/1/avatar');
    });

    it('falls back to the initial letter when there is no avatar', async () => {
        const container = await renderTable([challenger({ avatarUrl: null })]);

        expect(container.querySelector('img')).toBeNull();
        expect(container.textContent).toContain('A');
    });
});
```

- [ ] **Step 2: Run the test to verify it fails**

```bash
cd /Users/feor/projects/students/TOPAI/Practice/knights_fight/kf.web_ui
bun run test -- src/widgets/leaderboard/LeaderboardTable.test.tsx
```

Expected: FAIL — no `<img>` is rendered.

- [ ] **Step 3: Render the avatar in the leaderboard**

In `kf.web_ui/src/widgets/leaderboard/LeaderboardTable.tsx`, replace the letter-circle block:

```tsx
                    <div className="h-9 w-9 rounded-full bg-gradient-to-br from-indigo-400 to-purple-500 flex items-center justify-center text-white text-sm font-bold shrink-0">
                        {c.name.charAt(0).toUpperCase()}
                    </div>
```

with:

```tsx
                    {c.avatarUrl ? (
                        <img
                            src={c.avatarUrl}
                            alt=""
                            className="h-9 w-9 rounded-full object-cover shrink-0"
                        />
                    ) : (
                        <div className="h-9 w-9 rounded-full bg-gradient-to-br from-indigo-400 to-purple-500 flex items-center justify-center text-white text-sm font-bold shrink-0">
                            {c.name.charAt(0).toUpperCase()}
                        </div>
                    )}
```

- [ ] **Step 4: Render the avatar in the newcomers list**

In `kf.web_ui/src/widgets/newcomers/NewcomersList.tsx`, replace the letter-circle block:

```tsx
                    <div className="h-8 w-8 rounded-full bg-gradient-to-br from-indigo-400 to-purple-500 flex items-center justify-center text-white text-xs font-bold">
                        {c.name.charAt(0).toUpperCase()}
                    </div>
```

with:

```tsx
                    {c.avatarUrl ? (
                        <img
                            src={c.avatarUrl}
                            alt=""
                            className="h-8 w-8 rounded-full object-cover shrink-0"
                        />
                    ) : (
                        <div className="h-8 w-8 rounded-full bg-gradient-to-br from-indigo-400 to-purple-500 flex items-center justify-center text-white text-xs font-bold">
                            {c.name.charAt(0).toUpperCase()}
                        </div>
                    )}
```

- [ ] **Step 5: Run the test to verify it passes**

```bash
cd /Users/feor/projects/students/TOPAI/Practice/knights_fight/kf.web_ui
bun run test -- src/widgets/leaderboard/LeaderboardTable.test.tsx
```

Expected: PASS — 2 tests.

- [ ] **Step 6: Build, lint and run the whole web UI suite**

```bash
cd /Users/feor/projects/students/TOPAI/Practice/knights_fight/kf.web_ui
bun run test
bun run build
bun run lint
```

Expected: all green. `bun run build` type-checks before bundling, so a type error fails the build.

- [ ] **Step 7: Commit**

```bash
cd /Users/feor/projects/students/TOPAI/Practice/knights_fight
git add kf.web_ui/src/widgets/leaderboard/LeaderboardTable.tsx \
        kf.web_ui/src/widgets/leaderboard/LeaderboardTable.test.tsx \
        kf.web_ui/src/widgets/newcomers/NewcomersList.tsx
git commit -m "feat: show challenger avatars on the leaderboard and newcomers panel"
```

---

### Task 9: kf.client avatar support

**Files:**
- Modify: `kf.client/include/http/IHttpClient.h`
- Modify: `kf.client/include/http/CprHttpClient.h`
- Modify: `kf.client/src/http/CprHttpClient.cpp`
- Modify: `kf.client/include/crypto/Base64.h`
- Modify: `kf.client/src/crypto/Base64.cpp`
- Create: `kf.client/include/crypto/JwtPayload.h`
- Create: `kf.client/src/crypto/JwtPayload.cpp`
- Modify: `kf.client/include/services/ChallengerService.h`
- Modify: `kf.client/src/services/ChallengerService.cpp`
- Create: `kf.client/include/services/AvatarService.h`
- Create: `kf.client/src/services/AvatarService.cpp`
- Modify: `kf.client/include/cfg/AppConfig.h`
- Modify: `kf.client/include/cli/CliApp.h`
- Modify: `kf.client/src/cli/CliApp.cpp`
- Modify: `kf.client/CMakeLists.txt`

**Interfaces:**
- Consumes: the existing `http::IHttpClient`, `crypto::IRsaEncryptor`, `services::AuthService`.
- Produces:
  ```cpp
  namespace http {
      struct MultipartFile {
          std::string fieldName;
          std::string fileName;
          std::string contentType;
          std::vector<std::byte> data;
      };

      class IHttpClient {
          // ...
          [[nodiscard]] virtual auto postMultipart(std::string_view url,
                                                  const MultipartFile& file,
                                                  std::string_view bearerToken)
              -> std::expected<HttpResponse, std::string> = 0;
      };
  }

  namespace crypto {
      auto base64UrlDecode(std::string_view in) -> std::expected<std::string, std::string>;
      auto extractSubject(std::string_view jwt) -> std::expected<long, std::string>;
  }

  namespace services {
      class AvatarService {
      public:
          AvatarService(std::shared_ptr<http::IHttpClient> http, cfg::AppConfig cfg);

          [[nodiscard]] auto uploadAvatar(long challengerId,
                                         const std::string& imagePath,
                                         const std::string& jwt)
              -> std::expected<std::string, std::string>;
      };
  }
  ```

- [ ] **Step 1: Extend the HTTP interface**

In `kf.client/include/http/IHttpClient.h`, add `<vector>` and `<cstddef>` to the includes and add the struct and method:

```cpp
#include <cstddef>
#include <expected>
#include <string>
#include <string_view>
#include <vector>

namespace http {

    struct HttpResponse {
        int status{};
        std::string body;
    };

    /// A single file part of a multipart/form-data request.
    struct MultipartFile {
        std::string fieldName;
        std::string fileName;
        std::string contentType;
        std::vector<std::byte> data;
    };

    class IHttpClient {
    public:
        virtual ~IHttpClient() = default;

        [[nodiscard]] virtual auto get(std::string_view url)
            -> std::expected<HttpResponse, std::string> = 0;

        [[nodiscard]] virtual auto post(std::string_view url, std::string body,
                                        std::string_view contentType)
            -> std::expected<HttpResponse, std::string> = 0;

        /// Posts one file part as multipart/form-data with a bearer token.
        [[nodiscard]] virtual auto postMultipart(std::string_view url,
                                                const MultipartFile& file,
                                                std::string_view bearerToken)
            -> std::expected<HttpResponse, std::string> = 0;
    };

}
```

- [ ] **Step 2: Implement it with cpr**

In `kf.client/src/http/CprHttpClient.cpp`, add:

```cpp
    auto CprHttpClient::postMultipart(std::string_view url, const MultipartFile& file,
                                      std::string_view bearerToken)
        -> std::expected<HttpResponse, std::string>
    {
        // cpr::Buffer takes a pair of iterators, not a pointer and a length.
        auto buffer = cpr::Buffer{
            file.data.begin(),
            file.data.end(),
            file.fileName.c_str()
        };

        auto multipart = cpr::Multipart{
            cpr::Part{file.fieldName, buffer, file.contentType}
        };

        auto r = cpr::Post(cpr::Url{std::string{url}},
                           multipart,
                           cpr::Header{{"Authorization", "Bearer " + std::string{bearerToken}}});
        if (r.error.code != cpr::ErrorCode::OK) {
            return std::unexpected(std::format("POST {} failed: {}", url, r.error.message));
        }
        return HttpResponse{static_cast<int>(r.status_code), std::move(r.text)};
    }
```

Add the declaration to `kf.client/include/http/CprHttpClient.h`, following the shape of the existing `get`/`post` declarations:

```cpp
        [[nodiscard]] auto postMultipart(std::string_view url, const MultipartFile& file,
                                        std::string_view bearerToken)
            -> std::expected<HttpResponse, std::string> override;
```

- [ ] **Step 3: Add base64url decoding**

In `kf.client/include/crypto/Base64.h`, add the declaration:

```cpp
        /// Decodes base64url (the JWT flavour): '-' and '_' stand in for '+' and '/'.
        [[nodiscard]] auto base64UrlDecode(std::string_view in)
            -> std::expected<std::string, std::string>;
```

In `kf.client/src/crypto/Base64.cpp`, add:

```cpp
    auto base64UrlDecode(std::string_view in) -> std::expected<std::string, std::string> {
        std::string translated;
        translated.reserve(in.size());
        for (const char c : in) {
            if (c == '-') translated += '+';
            else if (c == '_') translated += '/';
            else translated += c;
        }

        const auto decoded = base64Decode(translated);
        if (!decoded) return std::unexpected(decoded.error());

        const auto& bytes = *decoded;
        return std::string(reinterpret_cast<const char*>(bytes.data()), bytes.size());
    }
```

- [ ] **Step 4: Read the challenger id out of a JWT**

Create `kf.client/include/crypto/JwtPayload.h`:

```cpp
//
// Created by Бесхмельницкий Антон on 07.11.2026.
//

#ifndef KF_CLIENT_JWTPAYLOAD_H
#define KF_CLIENT_JWTPAYLOAD_H

#include <expected>
#include <string>
#include <string_view>

namespace crypto {

    /// Reads the "sub" claim of a JWT - the id of the challenger it was issued for.
    [[nodiscard]] auto extractSubject(std::string_view jwt) -> std::expected<long, std::string>;

}

#endif //KF_CLIENT_JWTPAYLOAD_H
```

Create `kf.client/src/crypto/JwtPayload.cpp`:

```cpp
//
// Created by Бесхмельницкий Антон on 07.11.2026.
//

#include "crypto/JwtPayload.h"
#include "crypto/Base64.h"
#include <nlohmann/json.hpp>

namespace crypto {

    auto extractSubject(std::string_view jwt) -> std::expected<long, std::string> {
        const auto firstDot = jwt.find('.');
        if (firstDot == std::string_view::npos) return std::unexpected("JWT has no payload part");

        const auto secondDot = jwt.find('.', firstDot + 1);
        if (secondDot == std::string_view::npos) return std::unexpected("JWT payload is not terminated");

        const auto payload = base64UrlDecode(jwt.substr(firstDot + 1, secondDot - firstDot - 1));
        if (!payload) return std::unexpected(payload.error());

        const auto json = nlohmann::json::parse(*payload, nullptr, false);
        if (json.is_discarded()) return std::unexpected("JWT payload is not valid JSON");

        if (!json.contains("sub")) return std::unexpected("JWT payload has no 'sub' claim");

        // AuthService issues "sub" as a string, but a numeric one is accepted too.
        const auto& subject = json["sub"];
        if (subject.is_string()) {
            try {
                return std::stoll(subject.get<std::string>());
            } catch (const std::exception&) {
                return std::unexpected("JWT 'sub' claim is not a number");
            }
        }
        if (subject.is_number_integer()) {
            return subject.get<long>();
        }
        return std::unexpected("JWT 'sub' claim is not a number");
    }

}
```

- [ ] **Step 5: Parse the created challenger's id**

In `kf.client/include/services/ChallengerService.h`, add to the public section:

```cpp
        /// Reads the id of a newly created challenger from the create response.
        [[nodiscard]] static auto parseChallengerId(std::string_view responseBody)
            -> std::expected<long, std::string>;
```

In `kf.client/src/services/ChallengerService.cpp`, add:

```cpp
    auto ChallengerService::parseChallengerId(std::string_view responseBody)
        -> std::expected<long, std::string>
    {
        const auto json = nlohmann::json::parse(responseBody, nullptr, false);
        if (json.is_discarded()) return std::unexpected("Create response is not valid JSON");

        if (!json.contains("resultValue")) return std::unexpected("Create response has no resultValue");

        const auto& result = json["resultValue"];
        if (!result.contains("id")) return std::unexpected("Create response has no id");

        try {
            return result["id"].get<long>();
        } catch (const nlohmann::json::exception&) {
            return std::unexpected("Create response id is not a number");
        }
    }
```

- [ ] **Step 6: Add the avatar service**

Create `kf.client/include/services/AvatarService.h`:

```cpp
//
// Created by Бесхмельницкий Антон on 07.11.2026.
//

#ifndef KF_CLIENT_AVATARSERVICE_H
#define KF_CLIENT_AVATARSERVICE_H

#include "cfg/AppConfig.h"
#include "http/IHttpClient.h"
#include <expected>
#include <memory>
#include <string>

namespace services {

    // Uploads an avatar image for a challenger.
    class AvatarService {
    public:
        AvatarService(std::shared_ptr<http::IHttpClient> http, cfg::AppConfig cfg);

        [[nodiscard]] auto uploadAvatar(long challengerId, const std::string& imagePath,
                                        const std::string& jwt)
            -> std::expected<std::string, std::string>;

    private:
        std::shared_ptr<http::IHttpClient> http_;
        cfg::AppConfig cfg_;
    };

}

#endif //KF_CLIENT_AVATARSERVICE_H
```

Create `kf.client/src/services/AvatarService.cpp`:

```cpp
//
// Created by Бесхмельницкий Антон on 07.11.2026.
//

#include "services/AvatarService.h"
#include <cstddef>
#include <format>
#include <fstream>
#include <ios>
#include <iterator>
#include <string>
#include <vector>

namespace services {

    namespace {

        struct ImageType {
            const char* extension;
            const char* contentType;
        };

        // The extension is a convenience for the player - the server decides
        // what the file really is by looking at its magic bytes.
        std::optional<ImageType> imageTypeFor(const std::string& path)
        {
            const auto dot = path.find_last_of('.');
            if (dot == std::string::npos) return std::nullopt;

            std::string extension = path.substr(dot + 1);
            for (auto& c : extension) c = static_cast<char>(std::tolower(static_cast<unsigned char>(c)));

            if (extension == "png") return ImageType{".png", "image/png"};
            if (extension == "jpg" || extension == "jpeg") return ImageType{".jpg", "image/jpeg"};
            return std::nullopt;
        }

    }

    AvatarService::AvatarService(std::shared_ptr<http::IHttpClient> http, cfg::AppConfig cfg)
        : http_(std::move(http)),
          cfg_(std::move(cfg)) {}

    auto AvatarService::uploadAvatar(long challengerId, const std::string& imagePath,
                                     const std::string& jwt)
        -> std::expected<std::string, std::string>
    {
        const auto imageType = imageTypeFor(imagePath);
        if (!imageType) {
            return std::unexpected(std::format(
                "'{}' is not a PNG or JPG file. Use a .png, .jpg or .jpeg image.", imagePath));
        }

        std::ifstream file(imagePath, std::ios::binary);
        if (!file) return std::unexpected(std::format("Cannot open '{}'.", imagePath));

        const std::vector<char> raw((std::istreambuf_iterator<char>(file)),
                                    std::istreambuf_iterator<char>());
        if (raw.empty()) return std::unexpected(std::format("'{}' is empty.", imagePath));

        std::vector<std::byte> data(raw.size());
        for (std::size_t i = 0; i < raw.size(); ++i) {
            data[i] = static_cast<std::byte>(static_cast<unsigned char>(raw[i]));
        }

        // The template holds a literal "{id}" - std::format would read that as a
        // named argument and throw, so the placeholder is replaced by hand.
        auto url = cfg_.challengerAvatarUrl;
        const auto placeholder = url.find("{id}");
        if (placeholder == std::string::npos) {
            return std::unexpected("The avatar URL template has no '{id}' placeholder.");
        }
        url.replace(placeholder, 4, std::to_string(challengerId));

        const http::MultipartFile part{
            .fieldName = "avatar",
            .fileName = std::format("avatar{}", imageType->extension),
            .contentType = imageType->contentType,
            .data = std::move(data)
        };

        auto response = http_->postMultipart(url, part, jwt);
        if (!response) return std::unexpected(response.error());

        if (response->status < 200 || response->status >= 300) {
            return std::unexpected(std::format("Avatar upload failed (HTTP {})", response->status));
        }

        return response->body;
    }

}
```

Add `#include <optional>` and `#include <cctype>` to the includes.

- [ ] **Step 7: Add the URL template**

In `kf.client/include/cfg/AppConfig.h`, add:

```cpp
        std::string challengerAvatarUrl = BASE_URL + "/challengers/{id}/avatar";
```

- [ ] **Step 8: Wire the CLI commands**

The client dispatches through `cli::CliApp` (`kf.client/include/cli/CliApp.h`), whose commands are
`executeX(const std::vector<std::string>& args)` methods. `create` already reads `[name] [secret]`
positionally; the avatar is a third positional argument.

In `kf.client/include/cli/CliApp.h`, add the includes, the result type and the new method:

```cpp
#include "cfg/AppConfig.h"
#include <expected>
#include <span>
#include <string>
#include <string_view>
#include <vector>

namespace cli {

    // The result of a successful login: the challenger's id and its JWT.
    struct AuthenticatedChallenger {
        long id;
        std::string jwt;
    };

    class CliApp {
    public:
        explicit CliApp(cfg::AppConfig cfg = {});

        // Runs the CLI with given argc/argv arguments, returns exit code
        int run(int argc, char* argv[]);
        int run(std::span<const std::string_view> args);

        void printUsage(std::string_view programName) const;

    private:
        int executeCreate(const std::vector<std::string>& args);
        int executeLogin(const std::vector<std::string>& args);
        int executeSendCmd(const std::vector<std::string>& args);
        int executeAvatar(const std::vector<std::string>& args);

        // Logs in and returns the challenger's id together with its JWT.
        std::expected<AuthenticatedChallenger, std::string> authenticate(
            const std::string& name, const std::string& secret);

        cfg::AppConfig cfg_;
    };

} // namespace cli
```

In `kf.client/src/cli/CliApp.cpp`, add the includes:

```cpp
#include "crypto/JwtPayload.h"
#include "services/AvatarService.h"

#include <expected>
#include <string>
```

Add the `authenticate` helper and the `executeAvatar` command:

```cpp
    std::expected<AuthenticatedChallenger, std::string>
    CliApp::authenticate(const std::string& name, const std::string& secret) {
        auto http = std::make_shared<http::CprHttpClient>();
        auto serverEncryptor = std::make_shared<crypto::OpensslRsaEncryptor>();
        auto clientKeyManager = std::make_shared<crypto::ClientKeyManager>();
        auto keyService = std::make_shared<services::KeyService>(http, serverEncryptor, cfg_);
        services::AuthService auth(http, serverEncryptor, clientKeyManager, keyService, cfg_);

        if (auto r = clientKeyManager->generate(); !r) {
            return std::unexpected(std::format("Failed to generate client keys: {}", r.error()));
        }

        auto loginResult = auth.login(name, secret);
        if (!loginResult) {
            return std::unexpected(std::format("Login failed: {}", loginResult.error()));
        }

        const auto& login = loginResult.value();

        auto id = crypto::extractSubject(login.jwt);
        if (!id) {
            return std::unexpected(std::format("Cannot read the challenger id: {}", id.error()));
        }

        return AuthenticatedChallenger{*id, login.jwt};
    }

    int CliApp::executeAvatar(const std::vector<std::string>& args) {
        std::string name = args.size() > 0 ? args[0] : "Examinator";
        std::string secret = args.size() > 1 ? args[1] : "s3cret";
        std::string avatarPath = args.size() > 2 ? args[2] : "";

        if (avatarPath.empty()) {
            std::cerr << "Error: avatar requires <name> <secret> <image_file>\n";
            std::cerr << "Example: kf_client avatar Alice s3cret ./my-avatar.png\n";
            return 1;
        }

        std::cout << "1. Authenticating as '" << name << "'...\n";
        auto auth = authenticate(name, secret);
        if (!auth) {
            std::cerr << std::format("   [!] {}\n", auth.error());
            return 1;
        }
        std::cout << "   [✓] Authenticated.\n";

        auto http = std::make_shared<http::CprHttpClient>();
        services::AvatarService avatars(http, cfg_);

        std::cout << std::format("2. Uploading avatar '{}'...\n", avatarPath);
        auto uploaded = avatars.uploadAvatar(auth->id, avatarPath, auth->jwt);
        if (!uploaded) {
            std::cerr << std::format("   [!] Avatar upload failed: {}\n", uploaded.error());
            return 1;
        }

        std::cout << "   [✓] Avatar uploaded.\n";
        std::cout << "Server response:\n" << uploaded.value() << '\n';
        return 0;
    }
```

Extend `executeCreate` so an optional third positional argument is uploaded after the challenger
exists. Change its first two lines to read the avatar path, and append the upload before the
final `return 0;`:

```cpp
    int CliApp::executeCreate(const std::vector<std::string>& args) {
        std::string name = args.size() > 0 ? args[0] : "Examinator";
        std::string secret = args.size() > 1 ? args[1] : "s3cret";
        std::string avatarPath = args.size() > 2 ? args[2] : "";
```

```cpp
        if (avatarPath.empty()) return 0;

        auto id = services::ChallengerService::parseChallengerId(result.value());
        if (!id) {
            std::cerr << std::format("   [!] Cannot read the new challenger's id: {}\n", id.error());
            return 1;
        }

        std::cout << "3. Authenticating to upload the avatar...\n";
        auto auth = authenticate(name, secret);
        if (!auth) {
            std::cerr << std::format("   [!] {}\n", auth.error());
            return 1;
        }

        services::AvatarService avatars(http, cfg_);

        std::cout << std::format("4. Uploading avatar '{}'...\n", avatarPath);
        auto uploaded = avatars.uploadAvatar(*id, avatarPath, auth->jwt);
        if (!uploaded) {
            std::cerr << std::format("   [!] Avatar upload failed: {}\n", uploaded.error());
            return 1;
        }

        std::cout << "   [✓] Avatar uploaded.\n";
        return 0;
    }
```

Dispatch the new command in `CliApp::run`, next to the existing ones:

```cpp
        if (command == "avatar") {
            return executeAvatar(cmdArgs);
        }
```

Document it in `CliApp::printUsage`:

```cpp
        std::cerr << "  create   [name] [secret] [image_file]        Create a new challenger, optionally with an avatar\n";
        std::cerr << "  avatar   <name> <secret> <image_file>        Replace a challenger's avatar (PNG or JPG)\n";
```

and add an example line:

```cpp
        std::cerr << std::format("  {} create Alice s3cret ./my-avatar.png\n", programName);
        std::cerr << std::format("  {} avatar Alice s3cret ./my-avatar.png\n", programName);
```

- [ ] **Step 9: Register the new sources in CMake**

In `kf.client/CMakeLists.txt`, add to the `add_executable(kf_client ...)` source list:

```cmake
        src/crypto/JwtPayload.cpp
        src/services/AvatarService.cpp
```

- [ ] **Step 10: Build the client**

```bash
cd /Users/feor/projects/students/TOPAI/Practice/knights_fight/kf.client
cmake -S . -B build -DCMAKE_BUILD_TYPE=Release
cmake --build build -j
```

Expected: the build succeeds with no errors. Fix any `override` mismatch on `postMultipart` — the declaration in `CprHttpClient.h` needs `override` if the base declares it virtual.

- [ ] **Step 11: Verify the client against a running API**

With the database and the API up:

```bash
# a challenger with no avatar gets a random default
cd /Users/feor/projects/students/TOPAI/Practice/knights_fight
./kf.client/build/kf_client create Alice s3cret

# a challenger with an avatar uploads it
./kf.client/build/kf_client create Bob s3cret ./kf.api/Assets/avatars/avatar_05.png

# an existing challenger's avatar can be replaced
./kf.client/build/kf_client avatar Bob s3cret ./kf.api/Assets/avatars/avatar_09.png

# the avatar is served
curl -s -o /tmp/bob.png -w '%{http_code} %{content_type}\n' \
  "http://localhost:8080/api/challengers/2/avatar"
```

Expected: the first command creates a challenger; the second and third print `✓ Avatar uploaded.`; the curl prints `200 image/png`.

- [ ] **Step 12: Commit**

```bash
cd /Users/feor/projects/students/TOPAI/Practice/knights_fight
git add kf.client
git commit -m "feat: let kf.client choose and replace a challenger avatar"
```

---

### Task 10: Documentation

**Files:**
- Modify: `readme.md`
- Modify: `AGENTS.md`

**Interfaces:**
- Consumes: the finished feature.
- Produces: no code.

- [ ] **Step 1: Document the endpoints in readme.md**

In `readme.md`, add two rows to the *Implemented endpoints* table, right after the `GET | api/challengers/stream` row:

```markdown
| GET | `api/challengers/{id}/avatar` | A challenger's avatar (PNG) | none |
| POST | `api/challengers/{id}/avatar` | Upload or replace a challenger's avatar | **Bearer JWT** |
```

- [ ] **Step 2: Add an Avatars section to readme.md**

After the *Leaderboard scoring* section in `readme.md`, add:

```markdown
### Avatars

Every challenger has an avatar, shown on the leaderboard and in the newcomers panel.

- A player may upload a **PNG or JPG** when registering. The image is center-cropped to a square and scaled to **50×50 px**, then stored in the database as a PNG blob (`Challengers.AvatarBlob`).
- The format is decided by the file's magic bytes, never by the request's `Content-Type` header. Images larger than 4096×4096 are rejected before decoding; uploads are capped at 5 MB.
- A player who chooses nothing is assigned a **random one of ten default avatars** when the challenger is created. The defaults live in `kf.api/Assets/avatars/` and are embedded in the API; the row stores the index (`Challengers.DefaultAvatarIndex`).
- Avatars are served by `GET api/challengers/{id}/avatar` and carry an `ETag`.
- `POST api/challengers/{id}/avatar` replaces an avatar and needs a **Bearer JWT** whose `sub` claim is the challenger's id — the same JWT `POST api/Auth/login` returns.
```

- [ ] **Step 3: Note the second authorized route in AGENTS.md**

In `AGENTS.md`, in the *Authorization* section, change:

```markdown
**Authorization:** `[Authorize]` appears exactly once in the whole API (on `/api/test`).
```

to:

```markdown
**Authorization:** `[Authorize]` appears twice in the whole API: on `/api/test` and on
`POST api/challengers/{id}/avatar` (avatar uploads — the JWT's `sub` claim must equal the
challenger id in the route). Every other endpoint is anonymous — **including the ones the web UI
calls**.
```

and update the sentence that follows it:

```markdown
There is no `FallbackPolicy` and no global auth filter, so every other endpoint is anonymous. The
web UI needs no auth flow; the encrypted login/create flow belongs to `kf.client`.
```

- [ ] **Step 4: Verify the docs read correctly and commit**

```bash
cd /Users/feor/projects/students/TOPAI/Practice/knights_fight
grep -n "avatar" readme.md AGENTS.md
git add readme.md AGENTS.md
git commit -m "docs: document the challenger avatar feature"
```

Expected: the grep shows the new endpoint rows, the Avatars section and the updated authorization note.

---

## Final Verification

Run everything before opening the PR:

```bash
cd /Users/feor/projects/students/TOPAI/Practice/knights_fight

dotnet build kf.api/kf.api.csproj
dotnet test kf.api.Tests

cd kf.web_ui
bun run test
bun run build
bun run lint

cd ../kf.client
cmake --build build -j
```

Then confirm the feature end to end against a running stack:

```bash
cd /Users/feor/projects/students/TOPAI/Practice/knights_fight
docker compose -f dev.compose.yaml up -d
dotnet run --project kf.api/kf.api.csproj --urls http://localhost:8080

# the leaderboard carries an avatar URL
curl -s "http://localhost:8080/api/leaderboard?limit=3" | jq '.[].avatarUrl'

# the avatar itself is served as PNG
curl -s -o /tmp/a.png -w '%{http_code} %{content_type} %{size_download}\n' \
  "http://localhost:8080/api/challengers/1/avatar"

# the upload route rejects an anonymous caller
curl -s -o /dev/null -w '%{http_code}\n' -X POST \
  "http://localhost:8080/api/challengers/1/avatar" -F "avatar=@/tmp/a.png"
```

Expected: the leaderboard shows `/api/challengers/N/avatar`; the avatar is served as `200 image/png`; the anonymous upload returns `401`.

Then check the browser: the leaderboard shows **Top Fighters** with avatars next to each name, and the newcomers panel shows avatars too.
