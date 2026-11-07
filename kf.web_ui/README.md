# kf.web_ui

Browser client for KnightFight — leaderboard, newcomers, current matches and lobby.

Read-only spectator board: it calls **only anonymous** API endpoints and has no auth flow.

## Stack

React 19 · Vite 8 · TypeScript 7 · Tailwind 4 · Zustand · TanStack Query · ky · Vitest · oxlint

## Prerequisites

- [Bun](https://bun.sh) 1.3+ — the project uses Bun as its package manager and script runner
- The KnightFight API running locally on port 8080 (see the root `readme.md`)

## Commands

```bash
bun install       # install dependencies (writes bun.lock)
bun run dev       # vite dev server → http://localhost:5173
bun run build     # tsc -b && vite build → dist/
bun run test      # vitest run (one-shot)
bun run test:watch# vitest in watch mode
bun run lint      # oxlint
bun run preview   # serve the production build
```

`bun run build` runs the TypeScript project build first, so type errors fail the build before the bundler starts.

## Docker

The image is a two-stage build: Bun compiles the app, nginx serves the static output.

```bash
cd kf.web_ui
docker build -t kf.web_ui:latest .
docker run --rm -p 8081:80 kf.web_ui:latest
```

Then open <http://localhost:8081>.

| Stage | Base image | What it does |
|---|---|---|
| `build` | `oven/bun:1-alpine` | `bun install --frozen-lockfile` → `bun run build` → `dist/` |
| `runtime` | `nginx:alpine` | serves `dist/` with SPA fallback, immutable asset caching and a healthcheck |

`nginx.conf` (in this folder) configures the runtime stage:

- `/assets/*` — content-hashed bundles, cached for a year and marked immutable
- `/` — SPA fallback: any unknown path serves `index.html` so deep links work
- `/index.html` — never cached, so clients always pick up a new shell

The container listens on **80** and reports health via `HEALTHCHECK`.

## Project structure

Feature-first, with a barrel `index.ts` per feature folder.

```
src/
├── app/                  # shell: App, AppHeader, providers, view store
├── pages/
│   ├── main/             # MainPage — switches on the active view
│   └── dashboard/        # placeholder, currently unused
├── entities/
│   ├── challenger/       # model + api (paged newcomer list)
│   └── ranked-challenger/# model + api (leaderboard)
├── widgets/
│   ├── leaderboard/      # ranked table, skeleton, error state
│   ├── newcomers/        # floating panel, SSE merge, skeleton
│   ├── current-matches/  # live match cards
│   └── lobby/            # waiting-room count
├── shared/
│   ├── api/client.ts     # ky instance — prefix '/api', 10s timeout, 2 retries
│   ├── lib/cn.ts         # conditional class helper
│   ├── lib/sse.ts        # EventSource wrapper
│   └── ui/TabBar.tsx     # generic tab bar
└── main.tsx              # entry point — mounts src/app/App.tsx
```

`src/app/model/viewStore.ts` holds the active view (`leaderboard` | `matches` | `lobby`) in a Zustand store; `AppHeader` renders the tab bar from it and `MainPage` switches on it.

Tests sit next to the code they cover (`*.test.ts`) and run under Vitest with the `jsdom` environment.

## API calls

All requests go through `src/shared/api/client.ts`, a `ky` instance with `prefix: '/api'`.

| Widget | Call | Response |
|---|---|---|
| Leaderboard | `GET /api/leaderboard?limit=20` | `RankedChallenger[]` |
| Newcomers | `GET /api/challengers?page=0&page_size=5` | `{ items[], totalCount, page, pageSize }` |
| Newcomers (live) | `GET /api/challengers/stream` (SSE) | `data: { id, name, registeredAt, avatarUrl }` frames |
| Current Matches | `GET /api/matches` | `Match[]` |
| Lobby | `GET /api/lobby` | `{ waitingCount, queue }` |

### The leaderboard auto-refreshes

`LeaderboardWidget` polls `GET /api/leaderboard` every 5 seconds (`refetchInterval`) and shows a *"Updated HH:MM:SS"* stamp so the refresh is visible. Polling pauses while the tab is in the background (`refetchIntervalInBackground: false`), and the **Refresh** button forces an immediate reload.

The **ranking is owned by the API**, not the client: `LeaderboardGet` scores 3 points per win and 1 per draw, then orders by score, then wins, then id. Challengers who have never fought are included, sitting at the bottom with 0 points. Keep the formula in one place — the API — rather than restating it in the browser.

### Server-Sent Events and the dev proxy

The Vite dev proxy **buffers streaming responses**, so an `EventSource` pointed at `/api/...` never receives frames in development. `API_ORIGIN` in `src/shared/api/client.ts` therefore points the stream straight at the API (`http://<host>:8080`) when `import.meta.env.DEV` is true, and is empty in production, where the SPA and the API share an origin behind nginx.

nginx also needs `proxy_buffering off;` on the `/api/` location (already set in `compose-modules/nginx/configs/*.template`) or it will buffer the stream the same way.

The newcomers panel polls every 5 seconds as a fallback, so a dead stream degrades to polling rather than an empty panel.

## Backend contract

`kf.web_ui` is the third source of truth in this repo and currently disagrees with the other two:

- `readme.md` (root) — the design spec, planning `/auth`, `/signin`, `/leaderboard`, `/lobby`, `/matches`
- `kf.api/Infrastructure/ApiMap.cs` — the implemented contract
- `src/entities/*/api.ts` — this package's expectations

The three now agree on every route the UI calls. When adding or changing an endpoint, update `ApiMap.cs` first, then the client. Never invent URLs here that are not in `ApiMap.cs`.

`kf.api.Tests/EndpointContractTests.cs` asserts the live route table and the response shapes — it is the safety net for this alignment.

## Why Bun and not npm or Deno

Bun was chosen after running both against this project (identical dependencies, same machine):

| | Bun 1.3.13 | Deno 2.9.8 |
|---|---|---|
| install (131 packages) | 5.1s | 8.3s |
| `vite build` | 387ms | 389ms |
| `vitest run` | 1.5s | 2.2s |
| dev server ready | 195ms | 251ms |

Both work, but Bun keeps `package.json` as the only manifest — no `deno.json` — and does not drag in a second opinionated toolchain. Deno's `deno fmt` would reformat the whole codebase (2-space, 80-column, double quotes against this project's 4-space, single-quote style) and `deno check` / `deno lint` duplicate the existing `tsc` and oxlint.

`package.json` pins `"packageManager": "bun@1.3.13"` so Corepack and CI pick the right tool.
