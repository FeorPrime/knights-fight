# Challenger avatars — design

**Date:** 2026-11-07
**Status:** approved (awaiting implementation plan)

## Goal

Every challenger gets an avatar:

- A player may **choose** their own image when registering.
- The image must be **PNG or JPG** and is automatically **shrunk to 50×50 px**.
- It is **stored in the database as a blob**.
- If the player chooses nothing, a **random one of 10 default avatars** is assigned — and the assignment happens **at challenger creation**.
- The avatar is **visible on the leaderboard**.
- `kf.client` (the desktop CLI) offers the same choice.
- Released as a **separate feature** — own branch, own migration, own endpoints.

## Context

The API already carries avatar hooks that are never populated:

| Type | Field | Current value |
|---|---|---|
| `RankedChallengerResponse` | `AvatarUrl` | always `null` |
| `ChallengerItemResponse` | `AvatarUrl` | always `null` |
| `ChallengerNewcomerEvent` | `AvatarUrl` | always `null` |

The web UI models already declare `avatarUrl?: string | null`
(`kf.web_ui/src/entities/ranked-challenger/model.ts`,
`kf.web_ui/src/entities/challenger/model.ts`). This feature fills those in and renders them.

### The transport constraint

`POST api/challenger` carries an RSA-OAEP-SHA256 encrypted body. The client encrypts the
**entire** inner JSON in a **single** RSA operation (`kf.client/src/crypto/OpensslRsaEncryptor.cpp`),
and the server decrypts it the same way (`CryptoService.Decrypt`).

RSA-2048 with OAEP-SHA256 has a plaintext ceiling of
`256 - 2 * 32 - 2 = 190` bytes. A base64-encoded 50×50 PNG is 2–8 KB, so **an avatar cannot ride
inside the existing encrypted envelope**. The avatar therefore travels on its own request.

## Decisions

| Decision | Choice | Rationale |
|---|---|---|
| Transport | Separate multipart upload | Keeps the encrypted create envelope and its 190-byte budget untouched; image bytes travel as binary without 33 % base64 inflation. |
| Lifecycle | Fixed at creation **and** changeable later | A challenger can replace their avatar through a dedicated endpoint. |
| Default avatars | Referenced by index, served from bundled assets | Keeps the table small; one source of truth for the default art. |
| Image library | SkiaSharp (MIT) | Permissive licence, cross-platform, decodes PNG and JPEG, no native ImageMagick binaries in the image. |
| Upload authorization | Bearer JWT | The JWT already carries the challenger id as `sub` (`AuthService.GenerateToken`), so ownership checking is free and no new credential has to be invented. |

## Architecture

### Data model

Two columns are added to the `Challengers` table (`kf.api/Models/Entities/ChallengerModel.cs`):

```csharp
public byte[]? AvatarBlob { get; set; }      // custom avatar: 50×50 PNG bytes
public int? DefaultAvatarIndex { get; set; }  // 0..9 -> one of the 10 bundled defaults
```

A CHECK constraint keeps them mutually exclusive — exactly one is set for every challenger:

```sql
CHECK (("AvatarBlob" IS NOT NULL AND "DefaultAvatarIndex" IS NULL)
    OR ("AvatarBlob" IS NULL AND "DefaultAvatarIndex" IS NOT NULL))
```

Migration: `AddChallengerAvatar` — `AvatarBlob bytea NULL`, `DefaultAvatarIndex int NULL`, plus the
constraint.

The blob column holds **only the final 50×50 PNG**. The original upload is never persisted.

### Bundled default avatars

Ten 50×50 PNG files live in `kf.api/Assets/avatars/`:

```
avatar_01.png … avatar_10.png
```

Each one is a heraldic shield with a tinctured field, a metallic rim and a
heraldic charge, and the charge emoji — 🤺 🌹 🐎 ⚔️ 🪖 🏰 💃 🐷 🐱 🐉 — sits **in front of**
the shield as a sticker with a dark outline and a drop shadow.

Two more files live next to them:

| File | Role |
|---|---|
| `generate_avatars.py` | Stdlib-only regenerator for the ten PNGs. |
| `render_emoji_glyphs.swift` | Offline step that renders the ten emoji bitmaps (macOS, system emoji font). |
| `emoji/*.png` | The ten 64×64 RGBA glyph bitmaps the generator composites in front of each shield. |

The generator reads the glyph PNGs and never needs a font or an image library
at runtime, so it stays standard-library only.

They are generated once as committed assets and embedded into the assembly
(`<EmbeddedResource Include="Assets/avatars/*.png" />`) so they ship inside the Docker image
without depending on the working directory.

`DefaultAvatarProvider` (`kf.api/Infrastructure/Services/DefaultAvatarProvider.cs`) loads all ten
into memory at startup and exposes:

- `IReadOnlyList<byte[]> Avatars` — the ten PNG payloads.
- `byte[] GetBytes(int index)` — the payload for an index; throws if out of range.
- `int RandomIndex()` — a uniformly random index in `[0, 10)`.

The index is the only coupling between a challenger row and the asset set. The assets are
versioned in the repository, so an index keeps pointing at the same picture for as long as the
file is not edited. Editing an asset changes the picture for every challenger using it — that is
the accepted trade-off of referencing instead of copying.

### Image pipeline

`IImageService` / `AvatarImageService` (`kf.api/Infrastructure/Services/Images/`) owns the whole
"uploaded bytes → stored blob" transformation. One method, one job:

```csharp
Task<AvatarImageResult> NormalizeAsync(Stream input, CancellationToken ct);
```

Steps:

1. **Magic-byte check** before anything else. PNG is `89 50 4E 47 0D 0A 1A 0A`; JPEG is
   `FF D8 FF`. The request's `Content-Type` header is advisory only and is never trusted — a caller
   can label anything as `image/png`.
2. **Dimension guard.** Read the dimensions from `SKCodec.Info` *before* decoding the pixels, and
   reject anything larger than 4096×4096. Decoding first would let a small upload expand into
   gigabytes of bitmap.
3. **Center-crop to a square**, then scale to 50×50 with high-quality filtering. Cropping rather
   than stretching keeps the picture undistorted; a wide photo loses its left and right edges, a
   tall one loses its top and bottom.
4. **Re-encode as PNG.** Everything stored is PNG, whatever the upload was — one content type on
   the way out, no format bookkeeping.

Failures are reported as a typed result, not an exception: `UnsupportedFormat`,
`TooLarge`, `DecodeFailed`, `Empty`.

### Endpoints

New constant in `kf.api/Infrastructure/ApiMap.cs`:

```csharp
public const string AVATAR = $"{API_ROOT}/challengers/{{id}}/avatar";
```

| Method | URL | Auth | Purpose |
|---|---|---|---|
| GET | `api/challengers/{id}/avatar` | none | Serve the challenger's avatar as PNG. |
| POST | `api/challengers/{id}/avatar` | Bearer JWT | Upload or replace the challenger's avatar. |

**GET** is anonymous, because the leaderboard and the newcomers panel are anonymous reads and the
avatar is public data. It resolves the challenger, then:

- `AvatarBlob` is set → serve those bytes.
- otherwise → serve the bundled default for `DefaultAvatarIndex`.

Response: `Content-Type: image/png`, `ETag` derived from the **content** of the bytes being
served, `Cache-Control: public, max-age=300`. `If-None-Match` is honoured and answered with
`304`.

The `ETag` is a hash of the payload rather than the row's `UpdatedAt`, because `UpdatedAt` moves
every time the challenger wins or loses a fight — a content hash keeps the cached image valid for
as long as the picture itself is unchanged.

**POST** is authorized. The handler checks that the JWT's `sub` claim equals the `{id}` in the
route; a mismatch is `403`. The body is `multipart/form-data` with a single file part named
`avatar`; the request is capped at 5 MB. On success the challenger row is updated:
`AvatarBlob` = the normalized PNG, `DefaultAvatarIndex` = `NULL`.

This is the **second** `[Authorize]` route in the API (the first is `GET /api/test`). The
attribute is applied on the handler override and mirrored with an explicit
`.RequireAuthorization()` on the mapped route, so the requirement does not depend on metadata
inference.

Because the create path is unchanged, the flow for a player who picks an avatar is three
requests:

1. `POST api/challenger` (encrypted) → `201`. The body is the usual `ExceptResult` envelope —
   `{"resultState":"Success","problem":"","resultValue":{"id":12,"challengerName":"…"}}` — so the
   client reads the new challenger's id from `resultValue.id`.
2. `POST api/Auth/login` (encrypted) → JWT
3. `POST api/challengers/12/avatar` (multipart, `Authorization: Bearer <jwt>`) → `200`

A player who picks nothing stops after step 1 — the default was already assigned.

### Default assignment at creation

`ChallengerCreate` assigns the default **when the row is created**, as the feature requires:

- no avatar supplied → `DefaultAvatarIndex` = `DefaultAvatarProvider.RandomIndex()`,
  `AvatarBlob` = `null`.
- an avatar was supplied → the client uploads it after creation; the upload sets `AvatarBlob` and
  clears `DefaultAvatarIndex`.

The assignment is therefore visible on the row from the moment it exists, and the newcomer event
published at creation already carries a resolvable avatar URL.

### Responses

All three avatar fields become root-relative URLs:

```
/api/challengers/{id}/avatar
```

Root-relative resolves correctly in both environments: in development the Vite proxy forwards
`/api` to `:8080`, in production nginx routes `/api/` to the API. A path-relative URL would break
on any SPA route other than `/`.

- `RankedChallengerResponse.AvatarUrl` — leaderboard.
- `ChallengerItemResponse.AvatarUrl` — paged newcomers list.
- `ChallengerNewcomerEvent.AvatarUrl` — SSE newcomer frames.

The leaderboard query keeps selecting scalar columns only. Blob bytes never enter the leaderboard
payload; the browser fetches the image separately and caches it by `ETag`.

### Web UI

`kf.web_ui/src/widgets/leaderboard/LeaderboardTable.tsx`:

```tsx
{c.avatarUrl
    ? <img src={c.avatarUrl} alt="" className="h-9 w-9 rounded-full object-cover shrink-0" />
    : <div className="h-9 w-9 rounded-full bg-gradient-to-br from-indigo-400 to-purple-500 …">
          {c.name.charAt(0).toUpperCase()}
      </div>}
```

`kf.web_ui/src/widgets/newcomers/NewcomersList.tsx` gets the same branch at `h-8 w-8`.

The letter-circle fallback stays for any challenger whose `avatarUrl` is missing, so a partially
deployed API still renders.

### kf.client

**`IHttpClient`** gains one method. The interface stays free of cpr types:

```cpp
struct MultipartFile {
    std::string fieldName;
    std::string fileName;
    std::string contentType;
    std::vector<std::byte> data;
};

[[nodiscard]] virtual auto postMultipart(std::string_view url,
                                        const MultipartFile& file,
                                        std::string_view bearerToken)
    -> std::expected<HttpResponse, std::string> = 0;
```

**`CprHttpClient`** implements it with `cpr::Multipart` + `cpr::Buffer` + `cpr::Bearer` — cpr
1.10.5 already ships all three (`include/cpr/multipart.h`, `file.h`, `bearer.h`).

**`AvatarService`** (`kf.client/src/services/AvatarService.cpp`) reads the file from disk, checks
that the extension is `.png`, `.jpg` or `.jpeg`, and posts it. The extension check is a
convenience for the player; the server's magic-byte check is the real gate.

**CLI** (`kf.client/main.cpp`):

```
kf_client create <name> <secret> [avatar]   # create, then upload if an avatar was given
kf_client avatar <name> <secret> <avatar>   # replace an existing challenger's avatar
```

`create` with an avatar performs the three-request flow: create → login → upload. `avatar` logs in
and uploads. Both reuse the existing `AuthService::login`, which already returns the JWT.

**`AppConfig`** gains:

```cpp
std::string challengerAvatarUrl = BASE_URL + "/challengers/{id}/avatar";
```

### Release as a separate feature

The work is a self-contained change set — its own migration, its own endpoints, its own client
commands — developed on a dedicated branch so it can be merged or dropped independently of
everything else in flight. The migration is additive (two nullable columns) and therefore does not
conflict with the schema work already in progress on `feature/knights_fight`. The exact branch
base is decided at implementation time from what has actually been merged by then.

## Error handling

| Situation | Response |
|---|---|
| No such challenger (GET) | `404` |
| POST without a token | `401` |
| POST with a token for a different challenger | `403` |
| POST with no `avatar` file part | `400` |
| POST with a non-PNG/JPEG file | `400` — unsupported format |
| POST with an image larger than 4096×4096 | `400` — too large |
| POST over 5 MB | `413` |
| POST with a corrupt image | `400` — could not be decoded |

The create endpoint is unchanged: a duplicate name still returns `409`.

## Testing

**API unit tests** (`kf.api.Tests/`):

- `AvatarImageServiceTests` — a PNG is accepted and comes back as a 50×50 PNG; a JPEG is accepted
  and comes back as a 50×50 PNG; a text file is rejected as an unsupported format; an oversized
  image is rejected; the output decodes to exactly 50×50.
- `DefaultAvatarProviderTests` — ten assets load, every one is a valid PNG of 50×50, and
  `RandomIndex()` stays in `[0, 10)`.

**API contract tests** (`kf.api.Tests/EndpointContractTests.cs`):

- `GET /api/challengers/{id}/avatar` returns `200` with `image/png` for a challenger holding a
  custom blob, and for one referencing a default.
- `GET` for an unknown id returns `404`.
- `POST` without a token returns `401`.
- `POST` with a token for a different challenger returns `403`.
- `POST` with a text file returns `400`.
- `POST` with a valid PNG stores a 50×50 blob and clears `DefaultAvatarIndex`.
- `GET /api/leaderboard` returns a non-null `avatarUrl` pointing at the avatar route.

**Web UI tests** (`kf.web_ui`, Vitest):

- `LeaderboardTable` renders an `<img>` with the avatar URL when `avatarUrl` is set, and the
  letter-circle fallback when it is not.

**Client** — the desktop client has no service-level test harness today (only the standalone
`test_otp` target), so it is verified manually through the commands above.

## Documentation

- `readme.md` — the two new routes in the endpoint table, plus an *Avatars* section covering the
  50×50 rule, the accepted formats, the default assignment, and the storage model.
- `AGENTS.md` — note that the API now has a second `[Authorize]` route
  (`POST api/challengers/{id}/avatar`) and that avatar writes require a JWT.
- `kf.web_ui/README.md` — the response shapes are unchanged, so no edit is needed.

## Out of scope

- Changing the avatar from the web UI. The web UI is a read-only dashboard; only `kf.client`
  writes avatars.
- GIF, WebP, or SVG uploads — PNG and JPG only.
- Avatars larger than 50×50 being kept at full resolution. The stored blob is always exactly
  50×50.
- Avatars in the match view or the UDP game protocol.
