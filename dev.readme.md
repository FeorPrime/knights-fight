# Local development

## Prerequisites

| Tool | Version | Notes |
|---|---|---|
| .NET SDK | 10.0 | For the API and the test project |
| Docker | any | Runs Postgres |
| Bun | 1.3+ | Package manager and script runner for the web UI |
| CMake | ≥ 3.28 | Only for the desktop client |

`POSTGRES_PW` must be present in your **shell environment** — the compose file reads it from there. Do not read, write, or modify `.env`; set the variable in your shell instead.

```shell
export POSTGRES_PW='...'
```

## 1. Database

```bash
docker compose -f dev.compose.yaml up -d
docker compose -f dev.compose.yaml ps     # expect kf_db to be healthy
```

This starts only Postgres (the API is run from source in development). If the data volume is stale and you want a clean slate:

```bash
docker compose -f dev.compose.yaml down -v
```

## 2. API

```bash
dotnet run --project kf.api/kf.api.csproj --urls http://localhost:8080
```

The HTTP API listens on **8080**, and the UDP game protocol listens on **5050**. Migrations are applied automatically at startup. Watch the console for `Now listening on: http://localhost:8080` and `UDP server listening on port 5050`.

Verify it:

```bash
curl -s "http://localhost:8080/api/leaderboard?limit=20" | jq
curl -s "http://localhost:8080/api/challengers?page=0&page_size=5" | jq
curl -s "http://localhost:8080/api/matches" | jq
curl -s "http://localhost:8080/api/lobby" | jq
```

No auth header is needed for any of those routes.

## 3. Web UI

```bash
cd kf.web_ui
bun install        # writes bun.lock
bun run dev        # http://localhost:5173
bun run build      # tsc -b && vite build → dist/
bun run test       # vitest run
bun run lint       # oxlint
```

The Vite dev server proxies `/api` to `http://localhost:8080` — start the API first.

One exception: the **SSE stream** cannot go through the dev proxy, because Vite buffers streaming responses. The client points `EventSource` straight at the API in development (`API_ORIGIN` in `src/shared/api/client.ts`). The newcomers panel also polls every 5 s, so the panel still works if the stream does not.

## 4. Web UI container

```bash
cd kf.web_ui
docker build -t kf.web_ui:latest .
docker run --rm -p 8081:80 kf.web_ui:latest
```

Bun compiles the app in the build stage, nginx serves the static output in the runtime stage. The container exposes **80** and has a `HEALTHCHECK`.

## 5. Desktop client (optional)

```bash
cd kf.client
cmake -DCMAKE_MAKE_PROGRAM=$(which ninja) -DCMAKE_CXX_COMPILER=$(which clang++) -G Ninja -S . -B build
cmake --build build -j
./build/kf_client create Alice s3cret
```

Requires vcpkg packages: `curl openssl cpr nlohmann-json`.

## Ports cheat sheet

| Port | Protocol | What runs there |
|---|---|---|
| 8080 | HTTP | ASP.NET API |
| 5050 | UDP | Game command protocol (`UdpCommandService`) |
| 8081 | HTTP | Web UI container (`WEB_PORT` in compose) |
| 5173 | HTTP | Vite dev server |
| 5432 | TCP | Postgres (`127.0.0.1:5432` in dev) |
| 80 / 443 | HTTP(S) | nginx reverse proxy |

## Tests

```bash
dotnet test kf.api.Tests          # xUnit — unit tests + endpoint contract tests
cd kf.web_ui && bun run test      # Vitest — jsdom environment
cd kf.client && ./build/test_otp  # Base32 round-trip + RFC 4226 HOTP vectors
cd kf.web_ui && bun run lint      # oxlint
```

`kf.api.Tests/EndpointContractTests.cs` asserts the live route table and response shapes — run it after any API contract change.

## EF tools

`dotnet-ef` is required for DB manipulations.

```shell
dotnet tool install --global dotnet-ef
```

## Useful commands for the DB

| Task | Command |
|---|---|
| Install EF tools globally | `dotnet tool install --global dotnet-ef` |
| Create a migration | `dotnet ef migrations add InitialCreate` |
| Apply migrations | `dotnet ef database update` |
| List migrations | `dotnet ef migrations list` |
| Remove last migration | `dotnet ef migrations remove` |
| Generate SQL script | `dotnet ef migrations script` |
| Revert to specific migration | `dotnet ef database update MigrationName` |
| Scaffold from existing DB | `dotnet ef dbcontext scaffold "ConnectionString" Npgsql.EntityFrameworkCore.PostgreSQL` |

## Docker (full stack)

```bash
docker compose up -d
```

Runs Postgres, the API image, the web UI image and an nginx reverse proxy. nginx routes `/api/` to the API and everything else to the web UI, so the browser sees a single origin. In production nginx generates self-signed certs, or waits for you to place valid ones.

> ⚠️ WARNING — invalid certs (wrong domain, or expired) WILL BE DELETED from the `nginx/certs` folder.

The compose build paths match the current layout: `compose.yaml` points at `kf.api/Dockerfile` and `kf.web_ui/Dockerfile`.

## Troubleshooting

**API exits immediately with `POSTGRES_PW Is not set`** — the variable is read from the shell environment or `appsettings`, not from a file you should edit. Export it in the shell that launches the API.

**"Address already in use" on 8080** — another API instance is already running. Find it with `lsof -nP -iTCP:8080 -sTCP:LISTEN` and stop it, or reuse it.

**The leaderboard shows "Couldn't load the leaderboard"** — the API is not running on 8080, or the Vite proxy is pointed elsewhere. `vite.config.ts` proxies `/api` to `http://localhost:8080`.

**The newcomers panel updates but the timestamp never changes** — that panel is fed by SSE, and the dev proxy buffers streams. The panel falls back to 5 s polling; the clock in the leaderboard header is the reliable indicator that auto-refresh is running.

**`bun run build` fails with a type error** — the build runs `tsc -b` first on purpose. Fix the type error rather than skipping the typecheck.

**Curl returns 404 for a route you can see in the source** — check for singular/plural drift and remember that routing is case-insensitive but not plural-insensitive. `GET /api/challengers` is the live route; `GET /api/challenger` still works as a legacy alias.
