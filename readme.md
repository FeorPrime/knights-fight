# KnightFight

Turn-based tactics game. Two challengers fight on a shrinking 20×20 battlefield over a shared HTTP API.

The repository holds three clients and one server:

| Component | Stack | Role |
|---|---|---|
| `kf.api/` | ASP.NET Core 10, EF Core + Npgsql | HTTP API — challengers, auth, leaderboard, matches, lobby + the UDP game protocol |
| `kf.web_ui/` | React 19, Vite 8, Tailwind 4, Zustand, React Query | Browser client — leaderboard, newcomers, matches, lobby |
| `kf.client/` | C++23, CMake, OpenSSL, cpr | Desktop client — the only component that implements the encrypted auth flow |
| `compose.yaml` | Docker + nginx | Full stack: Postgres, API, web UI, reverse proxy |

---

## Quick start

```bash
# 1. Database — POSTGRES_PW must be set in the shell environment
docker compose -f dev.compose.yaml up -d

# 2. API — HTTP listens on 8080
dotnet run --project kf.api/kf.api.csproj --urls http://localhost:8080

# 3. Web UI — Bun is the package manager and script runner
cd kf.web_ui && bun install && bun run dev     # http://localhost:5173
```

The API applies its EF migrations on startup. See `dev.readme.md` for the full local workflow and `kf.client/readme.md` for the desktop client.

---

## HTTP API

### Implemented endpoints

Registered by reflection from `IApiMethod` implementations; every URL lives in `kf.api/Infrastructure/ApiMap.cs`.

| Method | URL | Purpose | Auth |
|---|---|---|---|
| GET | `api/Auth/public-key` | Server RSA-2048 public key (PEM) | none |
| POST | `api/Auth/login` | Authenticate → JWT + OTP seed | none (RSA-encrypted body) |
| POST | `api/challenger` | Register a challenger | none (RSA-encrypted body) |
| GET | `api/challengers` | Newest challengers first, paged (`page`, `page_size`) | none |
| GET | `api/challenger` | Legacy alias of `api/challengers` | none |
| GET | `api/challengers/stream` | SSE stream of newcomer events | none |
| GET | `api/challengers/{id}/avatar` | A challenger's avatar (PNG) | none |
| POST | `api/challengers/{id}/avatar` | Upload or replace a challenger's avatar | **Bearer JWT** |
| GET | `api/leaderboard` | Ranked challengers (`limit`, 1–100) | none |
| GET | `api/matches` | Live matches | none |
| GET | `api/matches/{id}` | A single live match | none |
| POST | `api/lobby` | Join the waiting list; returns the match id when a match starts | **Bearer JWT** |
| GET | `api/lobby` | Lobby status — waiting count and queue | none |
| GET | `/api/test` | Bearer-token check endpoint | **Bearer JWT** |
| GET | `/api/test-otp` | Validate a TOTP code | none |

`kf.api.Tests/EndpointContractTests.cs` asserts this route table and the response shapes against a live host.

### Leaderboard scoring

The API owns the ranking: **3 points per win, 1 per draw**, ordered by score, then wins, then id. Challengers who have never fought are included and sit at the bottom with 0 points.

### Avatars

Every challenger has an avatar, shown on the leaderboard and in the newcomers panel.

- A player may upload a **PNG or JPG** when registering or replacing an avatar via `kf.client`. The image is center-cropped to a square and scaled to **50×50 px**, then stored in the database as a PNG blob (`Challengers.AvatarBlob`).
- The format is decided by the file's magic bytes, never by the request's `Content-Type` header. Images larger than 4096×4096 are rejected before decoding; uploads are capped at 5 MB.
- A player who chooses nothing is assigned a **random one of ten default avatars** when the challenger is created. The defaults live in `kf.api/Assets/avatars/` and are embedded in the API; the row stores the index (`Challengers.DefaultAvatarIndex`).
- Avatars are served by `GET api/challengers/{id}/avatar` and carry an `ETag`.
- `POST api/challengers/{id}/avatar` replaces an avatar and needs a **Bearer JWT** whose `sub` claim is the challenger's id — the same JWT `POST api/Auth/login` returns.

### Original assignment endpoints → status

| Specified | Status | Implemented as |
|---|---|---|
| `POST /auth` | implemented, renamed | `POST api/Auth/login` |
| `POST /signin` | implemented, renamed | `POST api/challenger` |
| `GET /leaderboard` | implemented | `GET api/leaderboard` |
| `POST /lobby` | implemented | `POST api/lobby` |
| `GET /matches?{filter}` | implemented | `GET api/matches` |
| `GET /matches/{id}` | implemented | `GET api/matches/{id}` |

### Authorization

`[Authorize]` is applied to two routes — `GET /api/test` and `POST /api/lobby`. There is no fallback policy and no global auth filter, so every other endpoint is anonymous. The encrypted POST bodies carry their own protection: the payload is sealed with the server's RSA-OAEP-SHA256 public key, and the plaintext secret is hashed with SHA3-512 before storage.

The web UI therefore needs **no** auth flow. The full login flow (client keypair → encrypted login → JWT + OTP seed → TOTP) lives in `kf.client`.

### Ports

| Port | Protocol | Owner |
|---|---|---|
| 8080 | HTTP | ASP.NET API (`launchSettings.json`, Dockerfile, `kf.client` `AppConfig.h`) |
| 8081 | HTTP | Web UI container (`compose.yaml`, `WEB_PORT`) |
| 5050 | UDP | Game command protocol — `UdpCommandService` listens here |
| 5173 | HTTP | Vite dev server (`kf.web_ui`) |
| 80 / 443 | HTTP(S) | nginx reverse proxy (`compose-modules/nginx`) |

---

## Game design

### Battlefield and rules

- The battlefield is a square, 20 × 20.
- Whoever leaves the battlefield or reaches 0 hit points loses.
- Both challengers start with 3 hit points.
- Two challengers start from opposite corners — red and blue.
- Random bonuses are spawned on the field from the bonuses list.
- Each challenger starts the game with a random main weapon, a random offhand item, and no bonus.
- Each turn a challenger can move forward, turn 90°, use the main weapon, use the offhand item, or wait.
- After every 5 turns the battlefield shrinks by one row/column, from a random side.
- Each turn lasts 0.5 seconds.

### Matchmaking

- A challenger declares themselves ready with `POST api/lobby`.
- As soon as **two or more** challengers are ready, they are paired and a match starts.
  Four ready challengers start two matches at once.
- The challengers that have been ready the longest are paired first. An odd challenger out
  keeps waiting and is picked up by the next round.
- The pair is taken out of the ready queue as soon as the match starts.
- Matches live in memory on the API instance and are lost when it restarts.

### Weapons

**Main**

1. **sword** — swings in front 3 squares (left one, in front, right one)
2. **spear** — pierces 2 squares in front
3. **bow** — skips 2 squares in front and hits 2 squares after

**Offhand**

1. **shield** — ignores damage in front, if used
2. **the hummer** — pushes whatever is in front 2 squares away (no damage dealt)
3. **grappling hook** — catches whatever is in 3 squares in front and moves it 1 square towards you

### Bonuses

1. **heart** — additional hit point
2. **horse** — you now move over 1 square
3. **reverse** — you now move backwards
4. **ring** — whoever stands near you at the end of the turn goes 1 square away from you
5. **tp** — swap squares with the opponent
6. **bolt** — hit your opponent 1 time
7. **storm** — hit you and the opponent 1 time

---

## Game server protocol

**Over TCP (or HTTP):**

- `get state` — max 2 requests per second; returns the squares and whatever stands on them, challengers' HP, weapons and owned bonuses.

**Over UDP (port 5050):** implemented in `kf.api/Infrastructure/Services/UdpCommandService.cs`

- `turn` — turn your challenger 90° left or right
- `hit_main` — use the main weapon on the next turn
- `hit_off` — use the offhand item
- `move` — move forward

Each turn, the game renders its state into a viewable form.

**Packet rules**

- Every packet contains the challenger token obtained for the session.
- Every packet contains the match id.
- The match must already be started.
- Whenever a packet is received it is queued, but only 3 received packets enter the queue — all others are discarded.
- Each turn resolves 1 queued packet.
- An empty queue means wait.

---

## Roadmap

### MVP

- Two players join a match by the same id.
- Both get a synchronised packet with the time to start.
- Both can send their commands into the queue.
- The queue pops commands simultaneously.

MVP covers: spawn, sword / spear / shield, movement, hits.

### Next

- Wire the UDP game protocol to a live match loop — commands are received and authenticated, but not yet applied to game state.
- Persist match results so the leaderboard reflects real fights.
- A live match view for the web UI.

---

## Repository layout

```
knights_fight/
├── kf.api/               # ASP.NET Core 10 minimal API
│   ├── Features/         # one class per endpoint (Auth, Challenger, Leaderboard, Matches, Lobby, Test)
│   ├── Infrastructure/   # ApiMap, endpoint base classes, crypto, DbContext, UDP services
│   ├── Models/           # entities, requests, responses, mappers
│   └── Migrations/
├── kf.game/              # the game engine - fighters, weapons, bonuses, shrinking board,
│                        # plus GameManager (registry) and Matchmaker (pairing rule)
├── kf.game.Tests/        # xUnit — the engine and the pairing rule
├── kf.api.Tests/         # xUnit — unit tests + EndpointContractTests
├── kf.web_ui/            # React SPA (Bun + Vite + Tailwind)
│   ├── Dockerfile        # bun build → nginx serve
│   └── nginx.conf        # SPA fallback + cache policy
├── kf.client/            # C++23 desktop client
├── compose-modules/      # nginx + postgres compose fragments
├── compose.yaml          # full stack
├── dev.readme.md         # local development workflow
├── install.sh            # Linux auto-update & deployment cron script
└── AGENTS.md             # notes for AI coding agents
```

---

## Automated deployment (Linux)

An automated deployment script `install.sh` is provided for Linux hosts to continuously sync the repository and redeploy container services upon receiving new commits.

> **Note:** `install.sh` is designed exclusively for Linux environments.

### Usage

```bash
# Install cron job to check and auto-deploy updates every minute
./install.sh --install

# Manually trigger update check, rebuild, and container redeploy on new commits
./install.sh --update

# Uninstall and remove the scheduled cron job
./install.sh --uninstall
```

### Operation

- **Cron Schedule:** Runs every minute via `crontab`.
- **Commit Detection:** Compares Git commit hashes (`git rev-parse HEAD`) before and after `git pull`.
- **Container Redeployment:** If new commits are detected, executes `docker compose build` followed by `docker compose up -d --remove-orphans` to ensure maximum uptime and immediate reflection of updates with zero unnecessary rebuilds.

---

## Known gaps

- **The UDP game protocol is received but not applied.** `UdpCommandService` authenticates and logs commands; the actual game-state mutation is still a TODO in `CommandProcessor`.
- **Matches are not persisted.** They live in the API's in-memory `GameManager`, so a restart loses them and the leaderboard is not fed by real fights yet.
- The web UI's leaderboard shows no draws — the leaderboard response carries wins, losses and score, but not the draw count.
