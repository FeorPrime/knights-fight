# Lobby Match Start — Design Spec

**Date:** 2026-11-07
**Status:** Approved
**Feature:** When more than one player in the lobby is ready, a new match is started using `kf.game`.

---

## 1. Problem

The lobby accepts ready declarations but nothing ever happens with them. A challenger who
presses *ready* waits forever — there is no matchmaking, no match, and no game state. The
game engine already exists in `kf.game` (`GameManager.CreateGame`, a fully working 1v1
engine with weapons, bonuses and a shrinking board), but **the API cannot reach it**:
`kf.api/kf.api.csproj` has no `ProjectReference` to `kf.game`.

The gap is exactly one feature: *turn the ready set into a running match.*

## 2. Decisions

These were settled with the user before writing the spec.

| Decision | Choice | Rationale |
|---|---|---|
| Pairing rule | **Pair every ready player into separate games at once** | 4 ready players → 2 games in one pass. No waiting for a second trigger. |
| Where the logic lives | **Pairing service in `kf.game`; the API orchestrates** | The pairing rule is a domain rule and belongs next to the engine, unit-testable without the API's DI. The API owns the lobby state and the HTTP surface, so it orchestrates. |
| Match storage | **In-memory `GameManager` only** | Matches live exactly as long as the process. No EF migration. Consistent with `OtpSeedsProvider`, which is already an in-memory single-instance store. |
| Scope | **Match start only** | UDP command routing (`CommandProcessor` → `IGameManager.EnqueueAction`) and the turn loop stay a follow-up. |

## 3. Current state

### What exists

| Piece | Location | Note |
|---|---|---|
| Lobby ready state | `kf.api/Infrastructure/Services/OtpSeedsProvider.cs` | In-memory `ConcurrentDictionary<string, LoggedInUserSession>`, keyed by **challenger name**. `LoggedInUserSession` has `UserId`, `OtpSeed`, `IsReady`, `LastActiveAt`. |
| Lobby read model | `IOtpSeedsProvider.GetLobbyUsers()` → `LobbyUserDto` | `Name`, `IsReady`, `Status`, `LastActive`. |
| Ready endpoint | `kf.api/Features/Lobby/LobbyJoin.cs` | `POST api/lobby`, `[Authorize]`, body `{ChallengerName, IsReady}`. Calls `SetReady`. |
| Lobby status endpoint | `kf.api/Features/Lobby/LobbyStatus.cs` | `GET api/lobby` and `GET api/lobby/status`. |
| Game engine | `kf.game/Engine/Game.cs` | `Game : IGame`. Two fighters, 3 HP each, random weapon + offhand, 2 bonuses, 20×20 board, shrinks every 5 turns. |
| Game registry | `kf.game/Management/GameManager.cs` | `IGameManager.CreateGame(c1Id, c2Id, c1Name, c2Name, config, randomProvider)` → `IGame`. `ConcurrentDictionary` keyed by `Guid`. |
| Match endpoints | `kf.api/Features/Matches/MatchesGetAll.cs`, `MatchGetById.cs` | **Stubs.** `MatchesGetAll` returns an empty list; `MatchGetById` always 404s. |
| Match DTO | `kf.api/Models/Responses/MatchDto.cs` | `Id`, `Challenger1`, `Challenger2`, `Status`, `StartedAt` — already matches the web UI's `Match` type. |

### What does not exist

- No `ProjectReference` from `kf.api` to `kf.game`.
- No `IGameManager` in the DI container.
- No pairing rule anywhere.
- No match identifier reaching the player.
- `IGame` has **no `StartedAt`** — `MatchDto.StartedAt` has nothing to map from.

## 4. Architecture

Three layers, each with one job.

```
POST /api/lobby                       GET /api/matches
      │                                     ▲
      ▼                                     │
┌──────────────────┐   ready set    ┌──────────────────┐
│  LobbyJoin       │───────────────▶│  LobbyMatchService│
│  (API endpoint)  │                │  (API orchestrator)│
└──────────────────┘                └────────┬─────────┘
                                           │  1. read ready challengers
                                           │  2. IMatchmaker.PairUp  ──────┐
                                           │  3. IGameManager.CreateGame   │
                                           │  4. set the pair unready      │
                                           └─────────────────────────────────┘
                                                     │
                                    ┌────────────────▼─────────────────┐
                                    │  kf.game                        │
                                    │   Matchmaker  (pure pairing)    │
                                    │   GameManager (registry)        │
                                    │   Game / IGame (engine)         │
                                    └─────────────────────────────────┘
```

**Layer 1 — `kf.game`: the pairing rule.** A new `IMatchmaker` takes a list of ready
challengers and returns pairs. It is a **pure function** — no state, no clock, no DI. The
API owns the ready queue; the matchmaker only decides who plays whom.

**Layer 2 — `kf.api`: the orchestrator.** A new `ILobbyMatchService` reads the ready set,
asks the matchmaker for pairs, creates one game per pair through `IGameManager`, and takes
the matched players back out of the ready queue. It holds a `SemaphoreSlim` so that two
players pressing *ready* at the same instant cannot produce two identical matches.

**Layer 3 — the HTTP surface.** `POST /api/lobby` reports the match id to the player who
was matched. `GET /api/matches` and `GET /api/matches/{id}` serve the live games from the
manager — which lights up the web UI's existing *Current Matches* widget with **no UI
change at all**, because it already renders exactly `MatchDto`.

### Identity: the fighter id is the challenger name

The lobby store is keyed by name (`ForceAdd(challenger.ChallengerName, …)` in
`AuthGetTokens`), the UDP packet carries `ChallengerName`, and the OTP check is done by
name. So the fighter id inside `kf.game` is **the challenger name**, not the database Guid.
This keeps the whole pipeline name-keyed and makes the (separate) UDP wiring a
name-to-name lookup with no database round trip.

Names are unique — `AuthGetTokens` resolves a login with
`SingleOrDefaultAsync(ch => ch.ChallengerName == request.ChallengerName)`.

## 5. Components

### 5.1 `kf.game` — `Matchmaker` (new)

`kf.game/Management/Matchmaker.cs`

```csharp
public sealed record MatchCandidate(string Id, string Name, DateTime ReadySince);
public sealed record MatchPair(MatchCandidate First, MatchCandidate Second);

public interface IMatchmaker
{
    IReadOnlyList<MatchPair> PairUp(IReadOnlyList<MatchCandidate> candidates);
}
```

**Rule.** Sort by `ReadySince` ascending, tie-break by `Id` ordinal. Pair consecutive
entries: `(0,1)`, `(2,3)`, … A candidate with no partner is left out and keeps waiting.

| Ready players | Pairs | Games started | Still waiting |
|---|---|---|---|
| 0 | 0 | 0 | – |
| 1 | 0 | 0 | 1 |
| 2 | 1 | 1 | 0 |
| 3 | 1 | 1 | 1 (the newest) |
| 4 | 2 | 2 | 0 |

### 5.2 `kf.game` — `StartedAt` (new)

`IGame` gains `DateTime StartedAt { get; }`. `Game` sets it from a `TimeProvider` in the
constructor. `GameManager` takes an optional `TimeProvider` and passes it down, so tests can
pin the clock without touching the wall clock.

### 5.3 `kf.api` — `ReadySince` on the lobby session

`LoggedInUserSession` gains `DateTime? ReadySince`. `SetReady` sets it on a false→true
transition and clears it on true→false; repeated `SetReady(true)` keeps the original
timestamp, so a player who re-confirms is not pushed to the back of the queue.

`LobbyUserDto` and `LobbyPlayerDto` (the public mirror) both gain `DateTime? ReadySince`.

### 5.4 `kf.api` — `LobbyMatchService` (new)

`kf.api/Infrastructure/Services/ILobbyMatchService.cs` and `LobbyMatchService.cs`.

```csharp
public sealed record StartedMatch(Guid Id, string Challenger1, string Challenger2, string Status, DateTime StartedAt);

public sealed record MatchStartOutcome(IReadOnlyList<StartedMatch> StartedMatches)
{
    public StartedMatch? ForPlayer(string playerName);
    public string? OpponentFor(string playerName);
}

public interface ILobbyMatchService
{
    Task<MatchStartOutcome> TryStartMatchesAsync();
}
```

The service is registered as a **singleton** — it holds the `SemaphoreSlim` gate and must be
the same instance for every request.

### 5.5 `kf.api` — endpoint changes

| Endpoint | Change |
|---|---|
| `POST api/lobby` | Response gains `matchId` (`Guid?`) and `opponent` (`string?`). Populated when the caller was matched. |
| `GET api/lobby` | Response gains `readySince` on every player. |
| `GET api/matches` | Serves live games from `IGameManager`, newest first. Was an empty list. |
| `GET api/matches/{id}` | Serves one live game, 404 when unknown or unparseable. Was always 404. |

`LobbyJoinResponse` gains two **optional** parameters, so every existing construction site
(`new LobbyJoinResponse(false, "…")`) keeps compiling.

## 6. Data flow

```
Alice  POST /api/lobby {isReady:true}  ─┐
                                        ├─▶ LobbyJoin.Handler
Bob    POST /api/lobby {isReady:true}  ─┘        │
                                                 ├─▶ seedsProvider.SetReady(name, true)
                                                 │      → ReadySince = now
                                                 ├─▶ lobbyMatchService.TryStartMatchesAsync()
                                                 │      ├─ await _gate.WaitAsync()
                                                 │      ├─ ready = [Alice, Bob]
                                                 │      ├─ pairs = matchmaker.PairUp(ready)
                                                 │      │      → [MatchPair(Alice, Bob)]
                                                 │      ├─ game = gameManager.CreateGame(
                                                 │      │        "Alice", "Bob", "Alice", "Bob")
                                                 │      ├─ seedsProvider.SetReady(Alice, false)
                                                 │      └─ seedsProvider.SetReady(Bob, false)
                                                 └─▶ 200 {success:true, matchId:"…", opponent:"Bob"}
```

`GET /api/matches` then returns:

```json
[{ "id": "3f2a…", "challenger1": "Alice", "challenger2": "Bob",
   "status": "InProgress", "startedAt": "2026-11-07T12:00:00Z" }]
```

## 7. Concurrency

**The race.** Alice and Bob press *ready* within milliseconds of each other. Two requests
arrive; each reads the ready set; each sees two ready players; each creates a match. The
same fight now exists twice, and both players were reset to unready by whichever request
finished second.

**The fix.** `LobbyMatchService` serializes the whole read → pair → create → reset sequence
behind a `SemaphoreSlim(1, 1)`. The second request waits, then re-reads the ready set — by
then the pair has been reset to unready, so it starts nothing. Exactly one match exists.

The gate is in-process. That is sufficient because both the lobby store and the game
registry are in-process single-instance stores already (see the existing warning on
`OtpSeedsProvider`: *“This implementation is not for multi instance usage”*). Moving to
several API instances needs a distributed cache and a shared game registry — out of scope
here, and recorded as a known gap.

## 8. Error handling

| Situation | Behaviour |
|---|---|
| `ChallengerName` empty | `400` — unchanged. |
| Caller not logged in / session expired | `404` — unchanged. |
| Fewer than two ready players | `200`, no match started, `matchId` stays `null`. |
| Odd number of ready players | The unpaired player stays **ready** and is picked up by the next trigger. |
| `IGameManager.CreateGame` throws | The exception propagates; the pair stays ready and no partial match is recorded. `GameManager` only throws on a blank challenger id, which the lobby cannot produce. |

The endpoint never returns a 5xx for an ordinary "not enough players" case — that is a
normal lobby state, not an error.

## 9. Testing strategy

| Layer | Test | What it pins |
|---|---|---|
| `kf.game.Tests` | `MatchmakerTests` | 0/1/2/3/4 candidates; deterministic ordering; tie-break by id. |
| `kf.game.Tests` | `GameStartTimeTests` | `StartedAt` comes from the injected `TimeProvider`. |
| `kf.api.Tests` | `OtpSeedsProviderTests` (extended) | `ReadySince` is set on transition, kept on re-confirm, cleared on unready. |
| `kf.api.Tests` | `LobbyMatchServiceTests` (new) | 2 ready → 1 game; 4 ready → 2 games; 3 ready → 1 game + 1 still ready; concurrent calls → exactly one match. |
| `kf.api.Tests` | `EndpointContractTests` (extended) | `POST /api/lobby` returns `matchId`; `GET /api/matches` returns the started match; `GET /api/matches/{id}` returns it / 404s. |

Baseline at the time of writing: **184 API tests pass, 19 `kf.game` tests pass.**

## 10. Out of scope

Explicitly **not** part of this feature:

- Routing UDP commands into the game (`CommandProcessor` still logs instead of acting — the
  existing `TODO` in `CommandProcessor.cs` stays).
- The 0.5-second turn loop.
- Persisting matches to Postgres, and feeding `Wins`/`Draws` back into the leaderboard.
- Removing a challenger from the lobby when their match ends.
- The known authorization gap in `POST /api/lobby`: the request body names the challenger
  and the handler trusts it, so any authenticated caller can set *someone else's* ready
  flag. Worth fixing, but it is a separate change.

## 11. Known gaps created or left by this feature

- Matches live in memory only — an API restart loses them. Acceptable for the POC; the
  leaderboard is unaffected because it reads `Wins`/`Draws` from the database.
- The lobby store and the game registry are both in-process. More than one API instance
  would split the lobby and the games.
