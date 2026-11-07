# AGENTS.md

KnightFight — a turn-based tactics game: two challengers fight on a shrinking 20×20 battlefield over a shared HTTP API.

## Setup commands

```bash
# Database (dev) — POSTGRES_PW must be present in the shell environment
docker compose -f dev.compose.yaml up -d

# API — HTTP API listens on 8080
dotnet run --project kf.api/kf.api.csproj --urls http://localhost:8080

# API build / test
dotnet build kf.api/kf.api.csproj
dotnet test kf.api.Tests

# Web UI — http://localhost:5173 — Bun is the package manager and script runner
cd kf.web_ui
bun install
bun run dev        # vite dev server
bun run build      # tsc -b && vite build → dist/
bun run test       # vitest run
bun run lint       # oxlint

# Web UI container — bun builds, nginx serves
docker build -t kf.web_ui:latest kf.web_ui
docker run --rm -p 8081:80 kf.web_ui:latest

# Desktop client (C++23 / CMake, needs vcpkg: curl openssl cpr nlohmann-json)
cd kf.client && cmake -S . -B build -DCMAKE_BUILD_TYPE=Release && cmake --build build -j
```

## Project layout

- `kf.api/` — ASP.NET Core 10 minimal API (EF Core + Npgsql, JWT bearer, RSA/AES-GCM crypto, UDP game protocol)
- `kf.game/` — the game engine (fighters, weapons, bonuses, shrinking board) plus `GameManager` and the `Matchmaker`; `kf.api` references it
- `kf.game.Tests/` — xUnit tests for the engine
- `kf.api.Tests/` — xUnit test project (unit tests + `EndpointContractTests.cs`)
- `kf.web_ui/` — React 19 + Vite 8 + Tailwind 4 + Zustand + React Query SPA (Bun, Vitest)
- `kf.client/` — C++23 desktop client (CMake, OpenSSL, cpr, nlohmann-json)
- `compose-modules/` — nginx + postgres compose fragments; `compose.yaml` — full stack
- `readme.md` — **game design spec** (rules, weapons) and the project overview
- `.junie/plans/` — implementation plans (e.g. `force-bearer-token-check.md`)

## Architecture: three sources of truth — keep them aligned

The API surface is described in **three** places. They now agree; keep it that way:

1. **`readme.md`** — the design spec: rules, weapons, and the original endpoint list (`POST /auth`, `POST /signin`, `GET /leaderboard`, `POST /lobby`, `GET /matches`).
2. **`kf.api/Infrastructure/ApiMap.cs`** — the **implemented** HTTP contract. The single source of truth for what actually exists.
3. **`kf.web_ui/src/entities/*/api.ts`** — the client's expectations.

**When adding or changing an endpoint, update `ApiMap.cs` first, then the client — and never invent URLs in the frontend that aren't in `ApiMap.cs`.** `kf.api.Tests/EndpointContractTests.cs` asserts the live route table and response shapes; run it after any contract change.

### Ports

| Port | Protocol | Owner |
|---|---|---|
| 8080 | HTTP | ASP.NET API — `Properties/launchSettings.json`, Dockerfile EXPOSE, `kf.client/include/cfg/AppConfig.h`, nginx → `kf-api:8080` |
| 8081 | HTTP | Web UI container (`compose.yaml`, `WEB_PORT`) |
| 5050 | UDP | Game command protocol — `UdpCommandService`, `UdpSettings:Port` in `appsettings.json` |
| 5173 | HTTP | Vite dev server (`kf.web_ui`) |
| 80 / 443 | HTTP(S) | nginx reverse proxy — routes `/api/` to the API, `/` to the web UI |

HTTP never runs on 5050. `kf.web_ui/vite.config.ts` proxies `/api` → `http://localhost:8080`.

## API contract as implemented

| Method | URL | Defined in | Auth |
|---|---|---|---|
| GET | `api/Auth/public-key` | `ApiMap.Auth.GET_PUBLIC_KEY` | none |
| POST | `api/Auth/login` | `ApiMap.Auth.LOGIN` | none (RSA-OAEP-SHA256 envelope) |
| POST | `api/challenger` | `ApiMap.Challenger.CREATE` | none (RSA-OAEP-SHA256 envelope) |
| GET | `api/challengers` | `ApiMap.Challenger.GET_ALL` | none |
| GET | `api/challenger` | `ApiMap.Challenger.GET_ALL_LEGACY` | none (alias of `GET_ALL`) |
| GET | `api/challengers/stream` | `ApiMap.Challenger.STREAM` | none (SSE) |
| GET | `api/challengers/{id}/avatar` | `ApiMap.Challenger.AVATAR` | none |
| POST | `api/challengers/{id}/avatar` | `ApiMap.Challenger.AVATAR` | `[Authorize]` (Bearer JWT) |
| GET | `api/leaderboard` | `ApiMap.Leaderboard.GET_TOP` | none |
| GET | `api/matches` | `ApiMap.Match.GET_ALL` | none |
| GET | `api/matches/{id}` | `ApiMap.Match.GET_BY_ID` | none |
| POST | `api/lobby` | `ApiMap.Lobby.JOIN` | `[Authorize]` (Bearer JWT) |
| GET | `api/lobby` | `ApiMap.Lobby.STATUS` | none |
| GET | `/api/test` | literal, `Features/Test/Test.cs` | `[Authorize]` |
| GET | `/api/test-otp` | literal, `Features/Test/TestOtp.cs` | none |

Endpoints are registered by reflection: `ApiMapper.RegisterApiEndpoints()` scans for `IApiMethod` implementations, and `MapApiEndpoints()` maps each via `UriPattern`. **Adding a feature = add a class implementing `IApiMethod` and give it an `ApiMap` constant.** Routing is case-insensitive, so `api/Auth` vs `api/auth` does not matter — but singular vs plural does.

**Authorization:** `[Authorize]` appears on `/api/test`, `POST /api/lobby`, and `POST api/challengers/{id}/avatar` (where the JWT's `sub` claim must match the route `{id}`). There is no `FallbackPolicy` and no global auth filter, so all other endpoints are anonymous — **including the ones the web UI calls**. The web UI needs no auth flow; the encrypted login/create flow and avatar uploads belong to `kf.client`.

### Leaderboard scoring lives in the API

`Features/Leaderboard/LeaderboardGet.cs` scores **3 points per win, 1 per draw**, then orders by score, then wins, then id. Challengers who have never fought are included (0 points, at the bottom). `limit` is clamped to 1–100, default 20.

**Do not re-implement this in the client.** An earlier revision of the web UI ranked challengers in the browser; it was removed once the API endpoint landed, because two implementations of the same rule are exactly how this repo's contracts drifted apart before. The web UI only renders what `GET /api/leaderboard` returns.

## Web UI: what is already fixed — don't reintroduce these

- `@/*` path mapping lives in `tsconfig.app.json` (`paths`, **no** `baseUrl` — TypeScript 7 removed it) and mirrors the alias in `vite.config.ts`.
- The `src/shared/ui ` directory with the trailing space is gone; the folder is `src/shared/ui`.
- `react-error-boundary` and `@tanstack/react-query-devtools` are declared in `package.json`.
- The ky `beforeError` hook returns `state.error` (an `Error`), not the `BeforeErrorState` it receives. ky 2.x renamed `prefixUrl` → `prefix` — the client uses `prefix: '/api'`.
- The entry point is `src/app/App.tsx`, and `src/index.css` carries the single `@import "tailwindcss"`. The Vite template (`src/App.tsx`, `src/App.css`, `src/assets/*`, `public/icons.svg`) is deleted.
- Vitest is pinned to **v5** — Vitest 3 bundles its own rollup-based Vite and cannot type-check Vite 8's rolldown plugins.
- The Vite dev proxy targets `http://localhost:8080` (the HTTP API), not 5050 (the UDP game port).
- `vite.config.ts` uses `import.meta.dirname`, not `__dirname` — Vite 8's native config loader has no CommonJS globals.

### The dev proxy buffers SSE — the client works around it

The Vite dev proxy **buffers streaming responses**, so an `EventSource` on `/api/...` never receives frames in development. `API_ORIGIN` in `src/shared/api/client.ts` points the stream straight at the API (`http://<host>:8080`) when `import.meta.env.DEV`, and is empty in production where the SPA and API share an origin.

nginx has the same problem — `proxy_buffering off;` is required on the `/api/` location in `compose-modules/nginx/configs/*.template`, or it buffers the stream too.

The newcomers panel polls every 5 s as a fallback, so a dead stream degrades to polling rather than an empty panel.

## Build, test and run

```bash
cd kf.web_ui
bun install            # package manager + script runner
bun run dev            # dev server on :5173, proxies /api → :8080
bun run build          # tsc -b && vite build → dist/
bun run test           # vitest run (jsdom)
bun run lint           # oxlint
```

`bun run build` type-checks first, so type errors fail the build before bundling. Tests live next to the code (`*.test.ts`).

The container is two-stage: `oven/bun:1-alpine` installs with `--frozen-lockfile` and builds, then `nginx:alpine` serves `dist/` using the checked-in `nginx.conf` (SPA fallback, immutable asset caching, `no-cache` on `index.html`).

**Why Bun:** it was benchmarked against Deno on this project — install 5.1s vs 8.3s, `vitest run` 1.5s vs 2.2s — and it keeps `package.json` as the only manifest. Deno's `deno fmt` would reformat the whole codebase and `deno check`/`deno lint` duplicate `tsc` and oxlint.

## Manual verification

```bash
# 1. DB
docker compose -f dev.compose.yaml up -d

# 2. API on 8080
dotnet run --project kf.api/kf.api.csproj --urls http://localhost:8080

# 3. Every route the web UI calls
curl -s "http://localhost:8080/api/leaderboard?limit=20" | jq
curl -s "http://localhost:8080/api/challengers?page=0&page_size=5" | jq
curl -s "http://localhost:8080/api/matches" | jq
curl -s "http://localhost:8080/api/lobby" | jq

# 4. The SSE stream stays open (headers only, no body until an event fires)
curl -sN --max-time 3 -i "http://localhost:8080/api/challengers/stream" | head -8

# 5. UI
cd kf.web_ui && bun install && bun run dev
```

Browser check: DevTools → Network → Fetch/XHR. The leaderboard shows **Top Fighters** with rank badges and points, and a *"Updated HH:MM:SS"* stamp that changes every 5 seconds — that is the auto-refresh working. The newcomers panel lists the newest challengers with their registration time, and the Current Matches / Lobby tabs show live counts.

The encrypted POST flows (`POST /api/challenger`, `POST /api/Auth/login`) need an RSA-OAEP-SHA256 encrypted body and are only exercised by `kf.client` — plain `curl` will not do. Note the API deserializes those bodies with **default** `JsonSerializerOptions` (no camelCase policy), so the JSON property names must be PascalCase: `{"ChallengerName":"...","Secret":"..."}`.

## Code style

- **API:** C# 12 / .NET 10, file-scoped namespaces, primary constructors, `Nullable` enabled, `ImplicitUsings` on.
- **Namespaces vs folders.** The projects are `kf.api/` and `kf.api.Tests/`, but the C# namespaces are still `KnightFight.Api` / `KnightFight.Api.Tests` (pinned via `RootNamespace` in the csproj). Namespace does **not** match the folder name — do not "fix" it casually.
- **Endpoints:** inherit from a base in `Infrastructure/EndpointsMethods/` (`GetEndpoint`, `GetFilteredEndpoint`, `PostEndpoint`, `EncryptedPostEndpoint`, `StreamEndpoint`); put the URL in `ApiMap`, never inline it.
- **Binding attributes belong on the override.** The framework binds the most-derived override and does not inherit binding attributes from the base — `[AsParameters]` / `[FromBody]` must sit on the override. This is called out in comments in `GetFilteredEndpoint.cs` and `EncryptedPostEndpoint.cs`; follow it.
- **JSON:** camelCase for responses, source-generated via `AppJsonContext` (`JsonSourceGenerationOptions` with `PropertyNamingPolicy = CamelCase`). Request bodies decrypted from the envelope are deserialized with default options — PascalCase property names.
- **Web UI:** TypeScript strict, `verbatimModuleSyntax`, `noUnusedLocals`/`noUnusedParameters` on; 4-space indent, single quotes; Tailwind utility classes inline; `cn()` helper for conditional classes; feature-first folders `entities / widgets / pages / shared / app`; barrel `index.ts` per feature folder.
- **Crypto:** never roll your own — `CryptoService` (RSA-OAEP-SHA256 + AES-256-GCM), `OtpService`, `Base32` are the sanctioned primitives.

## Testing instructions

- API unit tests: `dotnet test kf.api.Tests` (xUnit; OTP RFC, Base32, auth service, OTP seed provider, command processor).
- API contract tests: `kf.api.Tests/EndpointContractTests.cs` asserts the live route table, the leaderboard ordering, the SSE stream and the paged challenger shape via `WebApplicationFactory`.
- Web UI: `cd kf.web_ui && bun run test` (Vitest, jsdom). Tests sit next to the code as `*.test.ts`.
- `kf.client/tests/test_otp.cpp` is a **separate CMake target** (`test_otp`, a standalone executable with its own `main()`) — but it is not registered with CTest, so `ctest` finds nothing. Run it directly: `./build/test_otp`.
- All tests must pass before opening a PR.

## PR & commit conventions

- **The git repository root is one level above this directory** — `/Users/feor/projects/students/TOPAI/Practice`, a container repo that also holds unrelated sibling projects (`bitmap/`, `idx_lst/`, `ist03_idx_lst/`). The repo-root `.gitignore` (which covers `*.env`, `build/`, `obj/`, `bin/`, `.junie/`) lives there too.
- `knights_fight/` is one project inside it. All commands in this file are relative to `knights_fight/`; git paths are relative to the repo root, so `git add AGENTS.md` from here stages `knights_fight/AGENTS.md`.
- Branch from `master` (the default branch); never push to it directly.
- Conventional commits (`feat:` / `fix:` / `docs:` / `refactor:`).
- No CI is configured in this repo — run the build, test and typecheck commands above locally before opening a PR.

## Security

- **Never read, write, or modify `.env`.** It holds `POSTGRES_PW` and is excluded from the Docker build via `.dockerignore`. Agents must leave it alone entirely — set the variable in the shell environment instead. Do not `cat`, `grep`, `source`, or echo its contents to inspect it.
- `.env` is correctly ignored by `*.env` in the repo-root `.gitignore`. Keep it that way — do not commit it or add a negation rule.
- Never commit secrets. `CryptoService.STATIC_KEY` is generated per-process at startup and must not be hardcoded; the same applies to `CryptoService`'s in-memory RSA key.
- `POST /api/challenger` and `POST /api/Auth/login` accept RSA-OAEP-SHA256 encrypted payloads; the plaintext secret is hashed with SHA3-512 before storage (`CryptoService.GetHash`). Never log secrets or decrypted payloads.
- The web UI requires no authorization — it calls only anonymous endpoints. Do not add auth to the frontend speculatively; the authenticated client is `kf.client`.
- UDP commands authenticate with a per-challenger TOTP (`CommandProcessor.ValidateAuthenticationAsync`) and are rate-limited per challenger — keep both when extending the protocol.
