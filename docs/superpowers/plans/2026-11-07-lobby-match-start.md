# Lobby Match Start Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** When two or more challengers in the lobby are ready, pair them up and start a real match through `kf.game`, and make the started match visible to the player who was matched.

**Architecture:** `kf.game` gains a pure `IMatchmaker` that decides who plays whom (sorted by ready timestamp, paired consecutively, an odd player waits). The API gains a `LobbyMatchService` that orchestrates: read the ready set from `OtpSeedsProvider` → ask the matchmaker for pairs → create one game per pair through `IGameManager` → set the matched players back to unready. The whole sequence is serialized behind a `SemaphoreSlim` so two players pressing *ready* at the same moment cannot produce two identical matches. `POST /api/lobby` reports the match id to the matched player; `GET /api/matches` and `GET /api/matches/{id}` serve the live games, which lights up the web UI's existing *Current Matches* widget with no UI change.

**Tech Stack:** ASP.NET Core 10 minimal API, EF Core 10 + Npgsql, xUnit, React 19 + Vite 8 + Vitest, C++23 + cpr 1.10.5.

**Spec:** [`docs/superpowers/specs/2026-11-07-lobby-match-start-design.md`](../specs/2026-11-07-lobby-match-start-design.md)

> **This plan was executed in a throwaway worktree before it was written up.** Every code
> block below compiled, and the whole flow was driven end to end against a live API:
> `dotnet test kf.api.Tests` → **198 passed**, `dotnet test kf.game.Tests` → **28 passed**,
> `bun run test` → **16 passed**, `bun run build` and `bun run lint` → clean, and the
> desktop client built and printed `⚔ Match started! id=… opponent=…` when the second
> challenger went ready. Treat the snippets as verified, not as sketches — but re-run the
> tests after you change them.
>
> Those counts were measured on the **pre-avatars** baseline (API 184, UI 16). The branch
> this plan runs on already carries the avatars feature, so its baseline is API **204**,
> game **19**, UI **18** — and the expected end state is API **218**, game **28**, UI
> **20**. The per-task expected counts below already use the avatars-inclusive numbers.
>
> Two known constraints found during that run: the xUnit analyzers reject
> `Assert.Empty(x.Where(...))` and `Assert.Single(x.Where(...))` — use
> `Assert.DoesNotContain(x, …)` and `Assert.Single(x, …)` instead (already applied below).
> And `AGENTS.md`'s API contract table and authorization note are **already correct** — the
> stale copy is in `readme.md`.

## Global Constraints

- **`kf.api` gains a `ProjectReference` to `kf.game`** in this feature. This is the first time the API depends on the engine.
- **The fighter id inside `kf.game` is the challenger name**, not the database Guid. The lobby store is keyed by name (`AuthGetTokens` → `ForceAdd(challenger.ChallengerName, …)`), the UDP packet carries `ChallengerName`, and the OTP check is by name. Keep the whole pipeline name-keyed.
- C# namespaces are `KnightFight.Api`, `KnightFight.Api.Tests`, `KnightFight.Game` and `KnightFight.Game.Tests` — **not** the folder names. `kf.api` uses `KnightFight.Api`, `kf.api.Tests` uses `KnightFight.Api.Tests`, `kf.game` uses `KnightFight.Game`, `kf.game.Tests` uses `KnightFight.Game.Tests`.
- API code style: C# 12, file-scoped namespaces, primary constructors, `Nullable` enabled, `ImplicitUsings` on.
- `kf.game.Tests` aliases the engine class: `global using GameSession = KnightFight.Game.Engine.Game;` — use `new GameSession(...)` in the game tests, never `new Game(...)` (it would collide with the xUnit `[Fact]`-adjacent `Game` name).
- Response JSON is **camelCase** — the default minimal-API `JsonSerializerDefaults.Web` options. `AppJsonContext` (`kf.api/Infrastructure/AppJsonContext.cs`) is the source-generated context used by the SSE stream and the encrypted request deserializer; register new **serialized** DTOs there too so the API stays consistent. Types that stay internal to the service layer (`StartedMatch`, `MatchStartOutcome`) are never serialized and need no registration.
- Request bodies are deserialized with **default** `JsonSerializerOptions` — **PascalCase** property names.
- `POST api/lobby` and `GET api/lobby` are the lobby routes. `api/lobby/status` is an alias of `GET api/lobby`.
- `LobbyJoinResponse` gains **optional** parameters so every existing construction site keeps compiling.
- Web UI style: TypeScript strict, `verbatimModuleSyntax`, **4-space indent**, **single quotes**, Tailwind utilities inline, `cn()` for conditional classes.
- Client style: C++23, `namespace` blocks indented inside the namespace.
- **Never read, write or modify `.env`.** `POSTGRES_PW` must be present in the shell environment for the API to start. Export it in the shell instead of inspecting the file.
- All tests must pass before opening a PR: `dotnet test kf.api.Tests`, `dotnet test kf.game.Tests`, `cd kf.web_ui && bun run test`, `bun run build`, `bun run lint`.
- **The repo root is one level up** (`/Users/feor/projects/students/TOPAI/Practice`), so from `knights_fight/` the git paths are `knights_fight/...`.

---

## Before You Start

The working tree is clean and the current branch is `feature/knights_fight` (tracking
`origin/feature/knights_fight`). Create a feature branch from its tip:

```bash
cd /Users/feor/projects/students/TOPAI/Practice/knights_fight
git switch -c feature/lobby-match-start
git status --short   # must be empty
```

**Baseline before you start** (both suites are green — verify and keep them green):

```bash
cd /Users/feor/projects/students/TOPAI/Practice/knights_fight
dotnet test kf.game.Tests    # 19 passed
dotnet test kf.api.Tests     # 204 passed
```

The API test suite needs Postgres on `127.0.0.1:5432` (`docker compose -f dev.compose.yaml up -d`)
and `POSTGRES_PW` in the shell environment.

---

## File Structure

**`kf.game` — new files**

| File | Responsibility |
|---|---|
| `kf.game/Management/Matchmaker.cs` | `MatchCandidate`, `MatchPair`, `IMatchmaker`, `Matchmaker`. The pure pairing rule. |

**`kf.game` — modified files**

| File | Change |
|---|---|
| `kf.game/Engine/IGame.cs` | Add `DateTime StartedAt { get; }`. |
| `kf.game/Engine/Game.cs` | Set `StartedAt` from a `TimeProvider`. |
| `kf.game/Management/GameManager.cs` | Hold a `TimeProvider` and pass it to each `Game`. |

**`kf.game.Tests` — new files**

| File | Responsibility |
|---|---|
| `kf.game.Tests/TestTimeProvider.cs` | A clock the test controls. |
| `kf.game.Tests/MatchmakerTests.cs` | The pairing rule. |
| `kf.game.Tests/GameStartTimeTests.cs` | `StartedAt` comes from the injected clock. |

**`kf.api` — new files**

| File | Responsibility |
|---|---|
| `kf.api/Infrastructure/Services/ILobbyMatchService.cs` | `StartedMatch`, `MatchStartOutcome`, `ILobbyMatchService`. |
| `kf.api/Infrastructure/Services/LobbyMatchService.cs` | The orchestrator: read → pair → create → reset, behind a gate. |

**`kf.api` — modified files**

| File | Change |
|---|---|
| `kf.api/kf.api.csproj` | `ProjectReference` to `kf.game`. |
| `kf.api/Program.cs` | Register `IMatchmaker`, `IGameManager`, `ILobbyMatchService`. |
| `kf.api/Infrastructure/Services/IOtpSeedsProvider.cs` | `LobbyUserDto` gains `DateTime? ReadySince`. |
| `kf.api/Infrastructure/Services/OtpSeedsProvider.cs` | `LoggedInUserSession.ReadySince`; `SetReady` maintains it. |
| `kf.api/Models/Responses/LobbyResponses.cs` | `LobbyPlayerDto` gains `DateTime? ReadySince`; `LobbyJoinResponse` gains `MatchId` + `Opponent`. |
| `kf.api/Features/Lobby/LobbyStatus.cs` | Pass `ReadySince` through. |
| `kf.api/Features/Lobby/LobbyJoin.cs` | Call the match service; report the match id. |
| `kf.api/Features/Matches/MatchesGetAll.cs` | Serve the live games. |
| `kf.api/Features/Matches/MatchGetById.cs` | Serve one live game; 404 when unknown. |

**`kf.api.Tests` — new / modified files**

| File | Responsibility |
|---|---|
| `kf.api.Tests/Infrastructure/Services/LobbyMatchServiceTests.cs` | The orchestration and the concurrency gate. |
| `kf.api.Tests/OtpSeedsProviderTests.cs` | Extend with `ReadySince` behaviour. |
| `kf.api.Tests/EndpointContractTests.cs` | Extend with the match-start contract. |

**Web UI — modified files**

| File | Change |
|---|---|
| `kf.web_ui/src/entities/lobby/model.ts` | `LobbyPlayer.readySince`; `LobbyJoinResponse.matchId` / `.opponent`. |
| `kf.web_ui/src/widgets/lobby/LobbyWidget.tsx` | Show when a fighter became ready. |
| `kf.web_ui/src/widgets/lobby/LobbyWidget.test.tsx` | Update the fixtures; assert the new line. |

**Client — modified files**

| File | Change |
|---|---|
| `kf.client/src/cli/CliApp.cpp` | Print the match id returned by `POST /api/lobby`. |

**Docs — modified files**

| File | Change |
|---|---|
| `readme.md` | Endpoint table, known gaps, roadmap. |
| `AGENTS.md` | API contract table; the `[Authorize]` note is wrong today — fix it. |

---

### Task 1: `StartedAt` on the game

The API has to report when a match started, and `MatchDto.StartedAt` has nothing to map
from today. `IGame` gains a start timestamp, taken from an injectable `TimeProvider` so
tests can pin it.

**Files:**
- Create: `kf.game.Tests/TestTimeProvider.cs`
- Modify: `kf.game/Engine/IGame.cs`
- Modify: `kf.game/Engine/Game.cs`
- Modify: `kf.game/Management/GameManager.cs`
- Test: `kf.game.Tests/GameStartTimeTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces:
  ```csharp
  namespace KnightFight.Game.Engine;

  public interface IGame
  {
      Guid Id { get; }
      DateTime StartedAt { get; }   // NEW
      // ...unchanged
  }

  public sealed class Game : IGame
  {
      public Game(
          string fighter1Id,
          string fighter2Id,
          string? fighter1Name = null,
          string? fighter2Name = null,
          GameConfig? config = null,
          IRandomProvider? randomProvider = null,
          Guid? gameId = null,
          TimeProvider? timeProvider = null);   // NEW last parameter
  }

  namespace KnightFight.Game.Management;

  public sealed class GameManager : IGameManager
  {
      public GameManager(TimeProvider? timeProvider = null);   // NEW
      // CreateGame signature unchanged
  }
  ```

- [ ] **Step 1: Create the test clock**

Create `kf.game.Tests/TestTimeProvider.cs`:

```csharp
namespace KnightFight.Game.Tests;

/// <summary>
/// A clock the test controls, so timestamps can be pinned instead of read from the wall.
/// </summary>
public sealed class TestTimeProvider : TimeProvider
{
    private DateTimeOffset _now;

    public TestTimeProvider(DateTimeOffset now) => _now = now;

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan delta) => _now = _now.Add(delta);
}
```

- [ ] **Step 2: Write the failing test**

Create `kf.game.Tests/GameStartTimeTests.cs`:

```csharp
using KnightFight.Game.Management;

namespace KnightFight.Game.Tests;

public class GameStartTimeTests
{
    private static readonly DateTimeOffset FixedNow =
        new(2026, 11, 7, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Game_StartedAt_ComesFromTheInjectedTimeProvider()
    {
        var clock = new TestTimeProvider(FixedNow);

        var game = new GameSession("alice", "bob", timeProvider: clock);

        Assert.Equal(new DateTime(2026, 11, 7, 12, 0, 0), game.StartedAt);
    }

    [Fact]
    public void GameManager_ForwardsItsTimeProviderToTheGameItCreates()
    {
        var clock = new TestTimeProvider(FixedNow);
        var manager = new GameManager(clock);

        var game = manager.CreateGame("alice", "bob");

        Assert.Equal(new DateTime(2026, 11, 7, 12, 0, 0), game.StartedAt);
    }
}
```

- [ ] **Step 3: Run the test to verify it fails**

```bash
cd /Users/feor/projects/students/TOPAI/Practice/knights_fight
dotnet test kf.game.Tests --filter "FullyQualifiedName~GameStartTimeTests"
```

Expected: FAIL — `Game` has no constructor parameter `timeProvider`, and `IGame` has no
`StartedAt`.

- [ ] **Step 4: Add `StartedAt` to the interface**

In `kf.game/Engine/IGame.cs`, add the property right after `Id`:

```csharp
public interface IGame
{
    Guid Id { get; }

    /// <summary>
    /// The moment the match was created, in UTC.
    /// </summary>
    DateTime StartedAt { get; }

    int TurnNumber { get; }
    // ...the rest is unchanged
}
```

- [ ] **Step 5: Set `StartedAt` in `Game`**

In `kf.game/Engine/Game.cs`, add the field next to `_config` and `_random`:

```csharp
private readonly object _syncRoot = new();
private readonly GameConfig _config;
private readonly IRandomProvider _random;
private readonly TimeProvider _time;
private readonly List<BonusEntity> _bonuses = [];

public Guid Id { get; }

/// <inheritdoc />
public DateTime StartedAt { get; }
```

Extend the constructor — the new parameter goes last so every existing call site keeps working:

```csharp
public Game(
    string fighter1Id,
    string fighter2Id,
    string? fighter1Name = null,
    string? fighter2Name = null,
    GameConfig? config = null,
    IRandomProvider? randomProvider = null,
    Guid? gameId = null,
    TimeProvider? timeProvider = null)
{
    _config = config ?? GameConfig.Default;
    _random = randomProvider ?? new DefaultRandomProvider();
    _time = timeProvider ?? TimeProvider.System;
    Id = gameId ?? Guid.NewGuid();
    StartedAt = _time.GetUtcNow().UtcDateTime;
    // ...the rest of the constructor is unchanged
}
```

- [ ] **Step 6: Let `GameManager` own the clock**

In `kf.game/Management/GameManager.cs`, add the field and constructor:

```csharp
private readonly ConcurrentDictionary<Guid, IGame> _games = new();
private readonly ConcurrentDictionary<string, ConcurrentDictionary<Guid, byte>> _challengerIndex = new(StringComparer.OrdinalIgnoreCase);
private readonly TimeProvider _timeProvider;

public GameManager(TimeProvider? timeProvider = null)
{
    _timeProvider = timeProvider ?? TimeProvider.System;
}
```

And pass it into the game inside `CreateGame` — `gameId` stays `null`:

```csharp
var game = new Engine.Game(
    challenger1Id,
    challenger2Id,
    challenger1Name,
    challenger2Name,
    config,
    randomProvider,
    null,          // gameId - let the game generate one
    _timeProvider  // NEW
);
```

- [ ] **Step 7: Run the test to verify it passes**

```bash
cd /Users/feor/projects/students/TOPAI/Practice/knights_fight
dotnet test kf.game.Tests
```

Expected: PASS — 21 tests (19 existing + 2 new).

- [ ] **Step 8: Commit**

```bash
cd /Users/feor/projects/students/TOPAI/Practice/knights_fight
git add kf.game/Engine/IGame.cs kf.game/Engine/Game.cs kf.game/Management/GameManager.cs \
        kf.game.Tests/TestTimeProvider.cs kf.game.Tests/GameStartTimeTests.cs
git commit -m "feat(game): expose StartedAt on the game and inject the clock"
```

---

### Task 2: The pairing rule (`Matchmaker`)

The rule lives in `kf.game` because it is a domain rule, and it lives in its own file
because it is a **pure function** — no state, no clock, no DI. The API owns the ready
queue; the matchmaker only decides who plays whom.

**Files:**
- Create: `kf.game/Management/Matchmaker.cs`
- Test: `kf.game.Tests/MatchmakerTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces:
  ```csharp
  namespace KnightFight.Game.Management;

  public sealed record MatchCandidate(string Id, string Name, DateTime ReadySince);

  public sealed record MatchPair(MatchCandidate First, MatchCandidate Second);

  public interface IMatchmaker
  {
      IReadOnlyList<MatchPair> PairUp(IReadOnlyList<MatchCandidate> candidates);
  }

  public sealed class Matchmaker : IMatchmaker
  {
      public IReadOnlyList<MatchPair> PairUp(IReadOnlyList<MatchCandidate> candidates);
  }
  ```

- [ ] **Step 1: Write the failing test**

Create `kf.game.Tests/MatchmakerTests.cs`:

```csharp
using KnightFight.Game.Management;

namespace KnightFight.Game.Tests;

public class MatchmakerTests
{
    private static readonly DateTime T0 = new(2026, 11, 7, 12, 0, 0, DateTimeKind.Utc);

    private readonly IMatchmaker _matchmaker = new Matchmaker();

    [Fact]
    public void PairUp_NoCandidates_ReturnsNoPairs()
    {
        Assert.Empty(_matchmaker.PairUp(Array.Empty<MatchCandidate>()));
    }

    [Fact]
    public void PairUp_OneCandidate_ReturnsNoPairs()
    {
        var pairs = _matchmaker.PairUp(new[] { Candidate("Alice", T0) });

        Assert.Empty(pairs);
    }

    [Fact]
    public void PairUp_TwoCandidates_PairsThem()
    {
        var pairs = _matchmaker.PairUp(new[]
        {
            Candidate("Alice", T0),
            Candidate("Bob", T0.AddSeconds(1))
        });

        var pair = Assert.Single(pairs);
        Assert.Equal("Alice", pair.First.Id);
        Assert.Equal("Bob", pair.Second.Id);
    }

    [Fact]
    public void PairUp_ThreeCandidates_PairsTheTwoOldestAndLeavesTheNewestWaiting()
    {
        var pairs = _matchmaker.PairUp(new[]
        {
            Candidate("Alice", T0),
            Candidate("Bob", T0.AddSeconds(1)),
            Candidate("Carol", T0.AddSeconds(2))
        });

        var pair = Assert.Single(pairs);
        Assert.Equal("Alice", pair.First.Id);
        Assert.Equal("Bob", pair.Second.Id);
    }

    [Fact]
    public void PairUp_FourCandidates_CreatesTwoGames()
    {
        var pairs = _matchmaker.PairUp(new[]
        {
            Candidate("Alice", T0),
            Candidate("Bob", T0.AddSeconds(1)),
            Candidate("Carol", T0.AddSeconds(2)),
            Candidate("Dave", T0.AddSeconds(3))
        });

        Assert.Equal(2, pairs.Count);
        Assert.Equal("Alice", pairs[0].First.Id);
        Assert.Equal("Bob", pairs[0].Second.Id);
        Assert.Equal("Carol", pairs[1].First.Id);
        Assert.Equal("Dave", pairs[1].Second.Id);
    }

    [Fact]
    public void PairUp_IgnoresTheOrderOfTheInput()
    {
        var ordered = new[] { Candidate("Alice", T0), Candidate("Bob", T0.AddSeconds(1)) };
        var shuffled = new[] { Candidate("Bob", T0.AddSeconds(1)), Candidate("Alice", T0) };

        var fromOrdered = _matchmaker.PairUp(ordered);
        var fromShuffled = _matchmaker.PairUp(shuffled);

        Assert.Equal(fromOrdered[0].First.Id, fromShuffled[0].First.Id);
        Assert.Equal(fromOrdered[0].Second.Id, fromShuffled[0].Second.Id);
    }

    [Fact]
    public void PairUp_SameReadyTime_BreaksTheTieById()
    {
        var pairs = _matchmaker.PairUp(new[]
        {
            Candidate("Bob", T0),
            Candidate("Alice", T0)
        });

        var pair = Assert.Single(pairs);
        Assert.Equal("Alice", pair.First.Id);
        Assert.Equal("Bob", pair.Second.Id);
    }

    private static MatchCandidate Candidate(string id, DateTime readySince) =>
        new(id, id, readySince);
}
```

- [ ] **Step 2: Run the test to verify it fails**

```bash
cd /Users/feor/projects/students/TOPAI/Practice/knights_fight
dotnet test kf.game.Tests --filter "FullyQualifiedName~MatchmakerTests"
```

Expected: FAIL — `KnightFight.Game.Management.IMatchmaker` does not exist.

- [ ] **Step 3: Write the implementation**

Create `kf.game/Management/Matchmaker.cs`:

```csharp
namespace KnightFight.Game.Management;

/// <summary>
/// A challenger who has declared themselves ready to fight.
/// </summary>
/// <param name="Id">Stable identifier. In the API this is the challenger name.</param>
/// <param name="Name">Display name.</param>
/// <param name="ReadySince">When the challenger declared themselves ready.</param>
public sealed record MatchCandidate(string Id, string Name, DateTime ReadySince);

/// <summary>
/// Two challengers that will fight each other.
/// </summary>
public sealed record MatchPair(MatchCandidate First, MatchCandidate Second);

/// <summary>
/// Turns a set of ready challengers into one-on-one pairs.
/// </summary>
public interface IMatchmaker
{
    /// <summary>
    /// Pairs the given candidates. The challengers that have been ready the longest are
    /// paired first. A candidate without a partner is left out of the result and keeps
    /// waiting for the next call.
    /// </summary>
    IReadOnlyList<MatchPair> PairUp(IReadOnlyList<MatchCandidate> candidates);
}

/// <inheritdoc cref="IMatchmaker" />
public sealed class Matchmaker : IMatchmaker
{
    public IReadOnlyList<MatchPair> PairUp(IReadOnlyList<MatchCandidate> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        if (candidates.Count < 2)
        {
            return Array.Empty<MatchPair>();
        }

        // The ready set arrives in dictionary order, which is not stable - sort it so the
        // same lobby always produces the same pairing. The id breaks a tie on the clock.
        var ordered = candidates
            .OrderBy(c => c.ReadySince)
            .ThenBy(c => c.Id, StringComparer.Ordinal)
            .ToArray();

        var pairs = new List<MatchPair>(ordered.Length / 2);
        for (var i = 0; i + 1 < ordered.Length; i += 2)
        {
            pairs.Add(new MatchPair(ordered[i], ordered[i + 1]));
        }

        // An odd candidate out has no partner and stays ready for the next round.
        return pairs;
    }
}
```

- [ ] **Step 4: Run the test to verify it passes**

```bash
cd /Users/feor/projects/students/TOPAI/Practice/knights_fight
dotnet test kf.game.Tests
```

Expected: PASS — 28 tests.

- [ ] **Step 5: Commit**

```bash
cd /Users/feor/projects/students/TOPAI/Practice/knights_fight
git add kf.game/Management/Matchmaker.cs kf.game.Tests/MatchmakerTests.cs
git commit -m "feat(game): add the matchmaker that pairs ready challengers"
```

---

### Task 3: Wire the API to `kf.game`, and track when a challenger became ready

Two things happen here: `kf.api` gets its `ProjectReference` to `kf.game` and registers the
new services, and the lobby session learns *when* a challenger became ready. The timestamp
is what makes the pairing deterministic — without it the ready set would be paired in
dictionary order, which is not stable.

**Files:**
- Modify: `kf.api/kf.api.csproj`
- Modify: `kf.api/Program.cs`
- Modify: `kf.api/Infrastructure/Services/IOtpSeedsProvider.cs`
- Modify: `kf.api/Infrastructure/Services/OtpSeedsProvider.cs`
- Modify: `kf.api/Models/Responses/LobbyResponses.cs`
- Modify: `kf.api/Features/Lobby/LobbyStatus.cs`
- Test: `kf.api.Tests/OtpSeedsProviderTests.cs`

**Interfaces:**
- Consumes: `IMatchmaker` and `IGameManager` from `KnightFight.Game.Management` (Task 1, Task 2).
- Produces:
  ```csharp
  namespace KnightFight.Api.Infrastructure.Services;

  public record LobbyUserDto(
      string Name,
      bool IsReady,
      string Status,
      DateTime LastActive,
      DateTime? ReadySince);   // NEW

  namespace KnightFight.Api.Models.Responses;

  public record LobbyPlayerDto(
      string Name,
      bool IsReady,
      string Status,
      DateTime LastActive,
      DateTime? ReadySince);   // NEW
  ```

- [ ] **Step 1: Write the failing test**

Append to `kf.api.Tests/OtpSeedsProviderTests.cs`, inside the class (before the closing
brace and before the private `SettableTimeProvider` class):

```csharp
    [Fact]
    public async Task SetReady_FirstTime_StampsTheReadyMoment()
    {
        var provider = new OtpSeedsProvider();

        await provider.ForceAdd("Alice", "SEED-ALICE");
        await provider.SetReady("Alice", true);

        var users = await provider.GetLobbyUsers();
        var alice = Assert.Single(users);
        Assert.True(alice.IsReady);
        Assert.NotNull(alice.ReadySince);
    }

    [Fact]
    public async Task SetReady_CalledAgain_KeepsTheOriginalReadyMoment()
    {
        var clock = new SettableTimeProvider();
        var provider = new OtpSeedsProvider(TimeSpan.FromMinutes(5), clock);

        await provider.ForceAdd("Alice", "SEED-ALICE");
        await provider.SetReady("Alice", true);

        var firstStamp = (await provider.GetLobbyUsers()).Single().ReadySince;

        clock.Advance(TimeSpan.FromSeconds(30));
        await provider.SetReady("Alice", true);

        var secondStamp = (await provider.GetLobbyUsers()).Single().ReadySince;
        Assert.Equal(firstStamp, secondStamp);
    }

    [Fact]
    public async Task SetReady_False_ClearsTheReadyMoment()
    {
        var provider = new OtpSeedsProvider();

        await provider.ForceAdd("Alice", "SEED-ALICE");
        await provider.SetReady("Alice", true);
        await provider.SetReady("Alice", false);

        var users = await provider.GetLobbyUsers();
        var alice = Assert.Single(users);
        Assert.False(alice.IsReady);
        Assert.Null(alice.ReadySince);
    }
```

- [ ] **Step 2: Run the test to verify it fails**

```bash
cd /Users/feor/projects/students/TOPAI/Practice/knights_fight
dotnet test kf.api.Tests --filter "FullyQualifiedName~OtpSeedsProviderTests"
```

Expected: FAIL — `LobbyUserDto` has no `ReadySince` property.

- [ ] **Step 3: Add the `ProjectReference`**

In `kf.api/kf.api.csproj`, add a new item group before the closing `</Project>`:

```xml
  <ItemGroup>
    <ProjectReference Include="..\kf.game\kf.game.csproj" />
  </ItemGroup>
```

- [ ] **Step 4: Extend the session and the DTOs**

In `kf.api/Infrastructure/Services/OtpSeedsProvider.cs`, extend the session class:

```csharp
public class LoggedInUserSession
{
    public required string UserId { get; set; }
    public string? OtpSeed { get; set; }
    public bool IsReady { get; set; }

    /// <summary>
    /// When the challenger declared themselves ready. Null while they are not ready.
    /// </summary>
    public DateTime? ReadySince { get; set; }

    public DateTime LastActiveAt { get; set; }
}
```

In the same file, make `SetReady` maintain the timestamp — set it on the false→true
transition, keep it on a repeated confirmation, clear it on true→false:

```csharp
    public Task<ExceptResult<bool>> SetReady(string userId, bool isReady = true)
    {
        if (userId == null)
        {
            return Task.FromResult(ExceptResult<bool>.Failure(CHALLENGER_NOT_FOUND));
        }

        if (!_seeds.TryGetValue(userId, out var session))
        {
            return Task.FromResult(ExceptResult<bool>.Failure(CHALLENGER_NOT_FOUND));
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        if (now - session.LastActiveAt > _inactivityTimeout)
        {
            _seeds.TryRemove(userId, out _);
            return Task.FromResult(ExceptResult<bool>.Failure(CHALLENGER_NOT_FOUND));
        }

        var wasReady = session.IsReady;
        session.IsReady = isReady;
        session.ReadySince = isReady
            ? (wasReady ? session.ReadySince ?? now : now)
            : null;
        session.LastActiveAt = now;
        return Task.FromResult(ExceptResult<bool>.Success(true));
    }
```

And carry it out through `GetLobbyUsers`:

```csharp
                result.Add(new LobbyUserDto(
                    session.UserId,
                    session.IsReady,
                    session.IsReady ? "ready" : "unready",
                    session.LastActiveAt,
                    session.ReadySince
                ));
```

In `kf.api/Infrastructure/Services/IOtpSeedsProvider.cs`, extend the DTO:

```csharp
public record LobbyUserDto(
    string Name,
    bool IsReady,
    string Status,
    DateTime LastActive,
    DateTime? ReadySince
);
```

In `kf.api/Models/Responses/LobbyResponses.cs`, extend the public mirror:

```csharp
public record LobbyPlayerDto(
    string Name,
    bool IsReady,
    string Status,
    DateTime LastActive,
    DateTime? ReadySince
);
```

In `kf.api/Features/Lobby/LobbyStatus.cs`, pass it through:

```csharp
        var players = users.Select(u => new LobbyPlayerDto(
            u.Name,
            u.IsReady,
            u.Status,
            u.LastActive,
            u.ReadySince
        )).ToList();
```

- [ ] **Step 5: Register the new services**

In `kf.api/Program.cs`, add the using at the top:

```csharp
using KnightFight.Game.Management;
```

and the registrations after the existing service registrations:

```csharp
// Game services - the API is the first consumer of the kf.game engine.
builder.Services.AddSingleton<IMatchmaker, Matchmaker>();
builder.Services.AddSingleton<IGameManager>(_ => new GameManager());
```

> `IGameManager` is registered through a factory on purpose: `GameManager`'s constructor
> takes an optional `TimeProvider`, and the factory keeps the registration explicit instead
> of relying on the container resolving an optional parameter.

`ILobbyMatchService` is registered in Task 4, once the type exists — do not add it here.

- [ ] **Step 6: Run the test to verify it passes**

```bash
cd /Users/feor/projects/students/TOPAI/Practice/knights_fight
dotnet test kf.api.Tests
```

Expected: PASS — 207 tests (204 existing + 3 new).

- [ ] **Step 7: Commit**

```bash
cd /Users/feor/projects/students/TOPAI/Practice/knights_fight
git add kf.api/kf.api.csproj kf.api/Program.cs \
        kf.api/Infrastructure/Services/IOtpSeedsProvider.cs \
        kf.api/Infrastructure/Services/OtpSeedsProvider.cs \
        kf.api/Models/Responses/LobbyResponses.cs \
        kf.api/Features/Lobby/LobbyStatus.cs \
        kf.api.Tests/OtpSeedsProviderTests.cs
git commit -m "feat(api): reference kf.game and track when a challenger became ready"
```

---

### Task 4: The orchestrator (`LobbyMatchService`)

This is the heart of the feature: read the ready set, pair it, create the games, and take
the matched players back out of the queue — all as one indivisible step.

**Files:**
- Create: `kf.api/Infrastructure/Services/ILobbyMatchService.cs`
- Create: `kf.api/Infrastructure/Services/LobbyMatchService.cs`
- Modify: `kf.api/Program.cs`
- Test: `kf.api.Tests/Infrastructure/Services/LobbyMatchServiceTests.cs`

**Interfaces:**
- Consumes: `IMatchmaker.PairUp(...)` (Task 2), `IGameManager.CreateGame(...)` and
  `IGame.StartedAt` (Task 1), `IOtpSeedsProvider.GetLobbyUsers()` / `.SetReady(...)` (Task 3).
- Produces:
  ```csharp
  namespace KnightFight.Api.Infrastructure.Services;

  public sealed record StartedMatch(
      Guid Id,
      string Challenger1,
      string Challenger2,
      string Status,
      DateTime StartedAt);

  public sealed record MatchStartOutcome(IReadOnlyList<StartedMatch> StartedMatches)
  {
      public StartedMatch? ForPlayer(string playerName);
      public string? OpponentFor(string playerName);
  }

  public interface ILobbyMatchService
  {
      Task<MatchStartOutcome> TryStartMatchesAsync();
  }

  public sealed class LobbyMatchService(
      IOtpSeedsProvider seedsProvider,
      IMatchmaker matchmaker,
      IGameManager gameManager) : ILobbyMatchService
  {
      public Task<MatchStartOutcome> TryStartMatchesAsync();
  }
  ```

- [ ] **Step 1: Write the failing test**

Create `kf.api.Tests/Infrastructure/Services/LobbyMatchServiceTests.cs`:

```csharp
using KnightFight.Api.Infrastructure.Services;
using KnightFight.Game.Management;
using Xunit;

namespace KnightFight.Api.Tests.Infrastructure.Services;

/// <summary>
/// Behaviour tests for <see cref="LobbyMatchService"/> - the piece that turns the ready
/// set into running matches.
/// </summary>
public class LobbyMatchServiceTests
{
    private readonly IOtpSeedsProvider _seeds = new OtpSeedsProvider(TimeSpan.FromMinutes(5));
    private readonly GameManager _games = new();
    private readonly LobbyMatchService _service;

    public LobbyMatchServiceTests()
    {
        _service = new LobbyMatchService(_seeds, new Matchmaker(), _games);
    }

    [Fact]
    public async Task TryStartMatches_OneReadyPlayer_StartsNothing()
    {
        await Ready("Alice");

        var outcome = await _service.TryStartMatchesAsync();

        Assert.Empty(outcome.StartedMatches);
        Assert.Empty(_games.GetAllGames());
    }

    [Fact]
    public async Task TryStartMatches_TwoReadyPlayers_StartsOneMatch()
    {
        await Ready("Alice");
        await Ready("Bob");

        var outcome = await _service.TryStartMatchesAsync();

        var match = Assert.Single(outcome.StartedMatches);
        Assert.Equal("Alice", match.Challenger1);
        Assert.Equal("Bob", match.Challenger2);
        Assert.Equal("InProgress", match.Status);
        Assert.Single(_games.GetAllGames());
    }

    [Fact]
    public async Task TryStartMatches_TwoReadyPlayers_PutsThemInTheSameGame()
    {
        await Ready("Alice");
        await Ready("Bob");

        var outcome = await _service.TryStartMatchesAsync();

        var game = Assert.Single(_games.GetAllGames());
        Assert.True(game.HasChallenger("Alice"));
        Assert.True(game.HasChallenger("Bob"));
        Assert.NotNull(outcome.ForPlayer("Alice"));
        Assert.Equal("Bob", outcome.OpponentFor("Alice"));
        Assert.Equal("Alice", outcome.OpponentFor("Bob"));
    }

    [Fact]
    public async Task TryStartMatches_FourReadyPlayers_StartsTwoMatches()
    {
        await Ready("Alice");
        await Ready("Bob");
        await Ready("Carol");
        await Ready("Dave");

        var outcome = await _service.TryStartMatchesAsync();

        Assert.Equal(2, outcome.StartedMatches.Count);
        Assert.Equal(2, _games.GetAllGames().Count);
    }

    [Fact]
    public async Task TryStartMatches_ThreeReadyPlayers_LeavesTheNewestWaiting()
    {
        await Ready("Alice");
        await Ready("Bob");
        await Ready("Carol");

        var outcome = await _service.TryStartMatchesAsync();

        Assert.Single(outcome.StartedMatches);

        var lobby = await _seeds.GetLobbyUsers();
        var stillReady = Assert.Single(lobby, u => u.IsReady);
        Assert.Equal("Carol", stillReady.Name);
    }

    [Fact]
    public async Task TryStartMatches_AfterAMatch_ThePairIsNoLongerReady()
    {
        await Ready("Alice");
        await Ready("Bob");

        await _service.TryStartMatchesAsync();

        var lobby = await _seeds.GetLobbyUsers();
        Assert.DoesNotContain(lobby, u => u.IsReady);
    }

    [Fact]
    public async Task TryStartMatches_CalledAgain_StartsNothingNew()
    {
        await Ready("Alice");
        await Ready("Bob");

        await _service.TryStartMatchesAsync();
        var second = await _service.TryStartMatchesAsync();

        Assert.Empty(second.StartedMatches);
        Assert.Single(_games.GetAllGames());
    }

    [Fact]
    public async Task TryStartMatches_ConcurrentCalls_StartExactlyOneMatch()
    {
        // Two players pressing "ready" at the same moment must not create the same fight twice.
        await Ready("Alice");
        await Ready("Bob");

        var outcomes = await Task.WhenAll(
            Task.Run(() => _service.TryStartMatchesAsync()),
            Task.Run(() => _service.TryStartMatchesAsync()));

        Assert.Single(_games.GetAllGames());
        Assert.Single(outcomes.SelectMany(o => o.StartedMatches));
    }

    private async Task Ready(string name)
    {
        await _seeds.ForceAdd(name, $"SEED-{name}");
        var result = await _seeds.SetReady(name, true);
        Assert.True(result.IsSuccess);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

```bash
cd /Users/feor/projects/students/TOPAI/Practice/knights_fight
dotnet test kf.api.Tests --filter "FullyQualifiedName~LobbyMatchServiceTests"
```

Expected: FAIL — `KnightFight.Api.Infrastructure.Services.LobbyMatchService` does not exist.

- [ ] **Step 3: Write the contract**

Create `kf.api/Infrastructure/Services/ILobbyMatchService.cs`:

```csharp
namespace KnightFight.Api.Infrastructure.Services;

/// <summary>
/// A match that has just been created.
/// </summary>
public sealed record StartedMatch(
    Guid Id,
    string Challenger1,
    string Challenger2,
    string Status,
    DateTime StartedAt);

/// <summary>
/// The result of a match-start attempt.
/// </summary>
public sealed record MatchStartOutcome(IReadOnlyList<StartedMatch> StartedMatches)
{
    /// <summary>
    /// The match the given player was placed in, or null when they are still waiting.
    /// </summary>
    public StartedMatch? ForPlayer(string playerName) =>
        StartedMatches.FirstOrDefault(m =>
            string.Equals(m.Challenger1, playerName, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(m.Challenger2, playerName, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// The opponent of the given player, or null when they are still waiting.
    /// </summary>
    public string? OpponentFor(string playerName) => ForPlayer(playerName) switch
    {
        null => null,
        var match when string.Equals(match.Challenger1, playerName, StringComparison.OrdinalIgnoreCase)
            => match.Challenger2,
        var match => match.Challenger1
    };
}

/// <summary>
/// Turns the ready challengers in the lobby into running matches.
/// </summary>
public interface ILobbyMatchService
{
    /// <summary>
    /// Pairs every ready challenger into a match and starts it. Challengers without a
    /// partner keep waiting.
    /// </summary>
    Task<MatchStartOutcome> TryStartMatchesAsync();
}
```

- [ ] **Step 4: Write the orchestrator**

Create `kf.api/Infrastructure/Services/LobbyMatchService.cs`:

```csharp
using KnightFight.Game.Management;

namespace KnightFight.Api.Infrastructure.Services;

/// <inheritdoc cref="ILobbyMatchService" />
public sealed class LobbyMatchService(
    IOtpSeedsProvider seedsProvider,
    IMatchmaker matchmaker,
    IGameManager gameManager) : ILobbyMatchService
{
    // Two players can press "ready" at the same moment. Without this gate both requests
    // would read the same ready set and create the same fight twice.
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task<MatchStartOutcome> TryStartMatchesAsync()
    {
        await _gate.WaitAsync();
        try
        {
            var lobby = await seedsProvider.GetLobbyUsers();

            var ready = lobby
                .Where(u => u.IsReady)
                .Select(u => new MatchCandidate(u.Name, u.Name, u.ReadySince ?? u.LastActive))
                .ToList();

            if (ready.Count < 2)
            {
                return new MatchStartOutcome(Array.Empty<StartedMatch>());
            }

            var pairs = matchmaker.PairUp(ready);
            var started = new List<StartedMatch>(pairs.Count);

            foreach (var pair in pairs)
            {
                // The fighter id is the challenger name - the lobby store, the OTP check and
                // the UDP packet all use the name as well.
                var game = gameManager.CreateGame(
                    pair.First.Id,
                    pair.Second.Id,
                    pair.First.Name,
                    pair.Second.Name);

                started.Add(new StartedMatch(
                    game.Id,
                    pair.First.Name,
                    pair.Second.Name,
                    game.Status.ToString(),
                    game.StartedAt));

                // The pair is fighting now - they must not be picked again.
                await seedsProvider.SetReady(pair.First.Id, false);
                await seedsProvider.SetReady(pair.Second.Id, false);
            }

            return new MatchStartOutcome(started);
        }
        finally
        {
            _gate.Release();
        }
    }
}
```

- [ ] **Step 5: Register the service**

In `kf.api/Program.cs`, next to the other game registrations:

```csharp
builder.Services.AddSingleton<ILobbyMatchService, LobbyMatchService>();
```

The service is a **singleton** — it owns the `SemaphoreSlim` gate, and every request must
go through the same instance.

- [ ] **Step 6: Run the test to verify it passes**

```bash
cd /Users/feor/projects/students/TOPAI/Practice/knights_fight
dotnet test kf.api.Tests --filter "FullyQualifiedName~LobbyMatchServiceTests"
```

Expected: PASS — 8 tests.

- [ ] **Step 7: Run the whole suite**

```bash
cd /Users/feor/projects/students/TOPAI/Practice/knights_fight
dotnet test kf.api.Tests && dotnet test kf.game.Tests
```

Expected: PASS — 215 API tests, 28 game tests.

- [ ] **Step 8: Commit**

```bash
cd /Users/feor/projects/students/TOPAI/Practice/knights_fight
git add kf.api/Infrastructure/Services/ILobbyMatchService.cs \
        kf.api/Infrastructure/Services/LobbyMatchService.cs \
        kf.api/Program.cs \
        kf.api.Tests/Infrastructure/Services/LobbyMatchServiceTests.cs
git commit -m "feat(api): start a match when the lobby has two ready challengers"
```

---

### Task 5: Tell the matched player which match they are in

Without this the match exists but nobody can find it. `POST /api/lobby` now reports the
match id and the opponent to the caller who was matched.

**Files:**
- Modify: `kf.api/Models/Responses/LobbyResponses.cs`
- Modify: `kf.api/Features/Lobby/LobbyJoin.cs`
- Test: `kf.api.Tests/EndpointContractTests.cs`

**Interfaces:**
- Consumes: `ILobbyMatchService.TryStartMatchesAsync()` and `MatchStartOutcome`
  (Task 4).
- Produces:
  ```csharp
  namespace KnightFight.Api.Models.Responses;

  public record LobbyJoinResponse(
      bool Success,
      string Message,
      Guid? MatchId = null,      // NEW
      string? Opponent = null);  // NEW
  ```

- [ ] **Step 1: Write the failing test**

Append to `kf.api.Tests/EndpointContractTests.cs`, inside the class:

```csharp
    [Fact]
    public async Task Lobby_TwoReadyChallengers_StartAMatchAndReportItsId()
    {
        var seedsProvider = _factory.Services.GetRequiredService<IOtpSeedsProvider>();
        var authService = _factory.Services.GetRequiredService<IAuthService>();

        await seedsProvider.ForceAdd("MatchKnightA", "SEED-A");
        await seedsProvider.ForceAdd("MatchKnightB", "SEED-B");

        var token = authService.GenerateToken(new ChallengerModel
        {
            Id = 101,
            ChallengerName = "MatchKnightA",
            Secret = "secret"
        });

        // 1. The first challenger becomes ready - nobody to play against yet.
        var firstResponse = await PostLobbyAsync(token, "MatchKnightA", true);
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        var firstBody = await firstResponse.Content.ReadFromJsonAsync<LobbyJoinResponse>(JsonOptions);
        Assert.NotNull(firstBody);
        Assert.True(firstBody.Success);
        Assert.Null(firstBody.MatchId);

        // 2. The second challenger becomes ready - a match starts.
        var secondResponse = await PostLobbyAsync(token, "MatchKnightB", true);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        var secondBody = await secondResponse.Content.ReadFromJsonAsync<LobbyJoinResponse>(JsonOptions);
        Assert.NotNull(secondBody);
        Assert.True(secondBody.Success);
        Assert.NotNull(secondBody.MatchId);
        Assert.NotNull(secondBody.Opponent);
        Assert.Equal("MatchKnightA", secondBody.Opponent);
    }

    private async Task<HttpResponseMessage> PostLobbyAsync(string token, string challengerName, bool isReady)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/lobby")
        {
            Content = JsonContent.Create(new LobbyJoinRequest(challengerName, isReady))
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }
```

- [ ] **Step 2: Run the test to verify it fails**

```bash
cd /Users/feor/projects/students/TOPAI/Practice/knights_fight
dotnet test kf.api.Tests --filter "FullyQualifiedName~EndpointContractTests"
```

Expected: FAIL — `LobbyJoinResponse` has no `MatchId` property.

- [ ] **Step 3: Extend the response**

In `kf.api/Models/Responses/LobbyResponses.cs`:

```csharp
public record LobbyJoinResponse(
    bool Success,
    string Message,
    Guid? MatchId = null,
    string? Opponent = null);
```

The two new parameters are optional, so the existing two-argument constructions in
`LobbyJoin.cs` keep compiling.

- [ ] **Step 4: Report the match**

In `kf.api/Features/Lobby/LobbyJoin.cs`, add the dependency and use the outcome:

```csharp
using KnightFight.Api.Infrastructure;
using KnightFight.Api.Infrastructure.EndpointsMethods;
using KnightFight.Api.Infrastructure.Services;
using KnightFight.Api.Models.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace KnightFight.Api.Features.Lobby;

public class LobbyJoin(IOtpSeedsProvider seedsProvider, ILobbyMatchService matchService)
    : PostEndpoint<LobbyJoinRequest>(ApiMap.Lobby.JOIN)
{
    [Authorize]
    public override async Task<IResult> Handler([FromBody] LobbyJoinRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ChallengerName))
        {
            return Results.BadRequest(new LobbyJoinResponse(false, "ChallengerName is required."));
        }

        var result = await seedsProvider.SetReady(request.ChallengerName, request.IsReady);
        if (result.IsError)
        {
            return Results.NotFound(new LobbyJoinResponse(false, $"Challenger '{request.ChallengerName}' is not logged in or session expired."));
        }

        // Two ready challengers are enough to start a match.
        var outcome = await matchService.TryStartMatchesAsync();
        var myMatch = outcome.ForPlayer(request.ChallengerName);

        if (myMatch is not null)
        {
            var opponent = outcome.OpponentFor(request.ChallengerName);
            return Results.Ok(new LobbyJoinResponse(
                true,
                $"Match {myMatch.Id} started: {request.ChallengerName} vs {opponent}.",
                myMatch.Id,
                opponent));
        }

        var status = request.IsReady ? "ready" : "unready";
        var response = new LobbyJoinResponse(true, $"Challenger {request.ChallengerName} status updated to {status}.");
        return Results.Ok(response);
    }
}
```

- [ ] **Step 5: Run the test to verify it passes**

```bash
cd /Users/feor/projects/students/TOPAI/Practice/knights_fight
dotnet test kf.api.Tests
```

Expected: PASS — 216 tests.

- [ ] **Step 6: Commit**

```bash
cd /Users/feor/projects/students/TOPAI/Practice/knights_fight
git add kf.api/Models/Responses/LobbyResponses.cs kf.api/Features/Lobby/LobbyJoin.cs \
        kf.api.Tests/EndpointContractTests.cs
git commit -m "feat(api): report the started match id to the matched challenger"
```

---

### Task 6: Serve the live matches

`GET /api/matches` returns an empty list today and `GET /api/matches/{id}` always 404s.
Both now serve the games the lobby started. The web UI's *Current Matches* widget already
renders exactly this shape, so it lights up with no UI change.

**Files:**
- Modify: `kf.api/Features/Matches/MatchesGetAll.cs`
- Modify: `kf.api/Features/Matches/MatchGetById.cs`
- Test: `kf.api.Tests/EndpointContractTests.cs`

**Interfaces:**
- Consumes: `IGameManager.GetAllGames()`, `IGameManager.GetGame(Guid)` and `IGame.StartedAt`
  (Task 1).
- Produces: no new types — `MatchDto` already exists and is already registered in
  `AppJsonContext`.

- [ ] **Step 1: Write the failing test**

Append to `kf.api.Tests/EndpointContractTests.cs`, inside the class:

```csharp
    [Fact]
    public async Task GetMatches_ListsTheStartedMatch()
    {
        var seedsProvider = _factory.Services.GetRequiredService<IOtpSeedsProvider>();
        var authService = _factory.Services.GetRequiredService<IAuthService>();
        var matchService = _factory.Services.GetRequiredService<ILobbyMatchService>();

        await seedsProvider.ForceAdd("ListedKnightA", "SEED-A");
        await seedsProvider.ForceAdd("ListedKnightB", "SEED-B");
        await seedsProvider.SetReady("ListedKnightA", true);
        await seedsProvider.SetReady("ListedKnightB", true);

        var outcome = await matchService.TryStartMatchesAsync();
        var match = Assert.Single(outcome.StartedMatches);

        var response = await _client.GetAsync("/api/matches");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadFromJsonAsync<List<MatchDto>>(JsonOptions);
        Assert.NotNull(content);
        Assert.Contains(content, m => m.Id == match.Id.ToString()
                                      && m.Challenger1 == "ListedKnightA"
                                      && m.Challenger2 == "ListedKnightB"
                                      && m.Status == "InProgress");
    }

    [Fact]
    public async Task GetMatchById_ReturnsTheMatch_And404sForAnUnknownId()
    {
        var seedsProvider = _factory.Services.GetRequiredService<IOtpSeedsProvider>();
        var matchService = _factory.Services.GetRequiredService<ILobbyMatchService>();

        await seedsProvider.ForceAdd("ByIdKnightA", "SEED-A");
        await seedsProvider.ForceAdd("ByIdKnightB", "SEED-B");
        await seedsProvider.SetReady("ByIdKnightA", true);
        await seedsProvider.SetReady("ByIdKnightB", true);

        var outcome = await matchService.TryStartMatchesAsync();
        var match = Assert.Single(outcome.StartedMatches);

        var found = await _client.GetAsync($"/api/matches/{match.Id}");
        Assert.Equal(HttpStatusCode.OK, found.StatusCode);
        var body = await found.Content.ReadFromJsonAsync<MatchDto>(JsonOptions);
        Assert.NotNull(body);
        Assert.Equal(match.Id.ToString(), body.Id);

        var missing = await _client.GetAsync($"/api/matches/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }
```

- [ ] **Step 2: Run the test to verify it fails**

```bash
cd /Users/feor/projects/students/TOPAI/Practice/knights_fight
dotnet test kf.api.Tests --filter "FullyQualifiedName~EndpointContractTests"
```

Expected: FAIL — `GET /api/matches` returns an empty list and `GET /api/matches/{id}`
returns 404.

- [ ] **Step 3: Serve the list**

Replace the body of `kf.api/Features/Matches/MatchesGetAll.cs`:

```csharp
using KnightFight.Api.Infrastructure;
using KnightFight.Api.Infrastructure.EndpointsMethods;
using KnightFight.Api.Models.Responses;
using KnightFight.Game.Management;

namespace KnightFight.Api.Features.Matches;

public class MatchesGetAll(IGameManager gameManager) : GetEndpoint(ApiMap.Match.GET_ALL)
{
    public override Task<IResult> Handler()
    {
        var matches = gameManager.GetAllGames()
            .OrderByDescending(g => g.StartedAt)
            .Select(g => new MatchDto(
                g.Id.ToString(),
                g.Fighter1.Name,
                g.Fighter2.Name,
                g.Status.ToString(),
                g.StartedAt))
            .ToList();

        return Task.FromResult(Results.Ok(matches));
    }
}
```

- [ ] **Step 4: Serve one match**

Replace the body of `kf.api/Features/Matches/MatchGetById.cs`:

```csharp
using KnightFight.Api.Infrastructure;
using KnightFight.Api.Infrastructure.ApiMethods;
using KnightFight.Api.Models.Responses;
using KnightFight.Game.Management;
using Microsoft.AspNetCore.Http;

namespace KnightFight.Api.Features.Matches;

public class MatchGetById(IGameManager gameManager) : ApiMethod(ApiMap.Match.GET_BY_ID)
{
    public override void MapEndpoint(WebApplication app)
    {
        app.MapGet(UriPattern, (string id) => GetMatch(id));
    }

    private IResult GetMatch(string id)
    {
        if (!Guid.TryParse(id, out var gameId))
        {
            return Results.NotFound(new { message = $"Match {id} not found." });
        }

        var game = gameManager.GetGame(gameId);
        if (game is null)
        {
            return Results.NotFound(new { message = $"Match {id} not found." });
        }

        return Results.Ok(new MatchDto(
            game.Id.ToString(),
            game.Fighter1.Name,
            game.Fighter2.Name,
            game.Status.ToString(),
            game.StartedAt));
    }
}
```

- [ ] **Step 5: Run the test to verify it passes**

```bash
cd /Users/feor/projects/students/TOPAI/Practice/knights_fight
dotnet test kf.api.Tests
```

Expected: PASS — 218 tests.

- [ ] **Step 6: Commit**

```bash
cd /Users/feor/projects/students/TOPAI/Practice/knights_fight
git add kf.api/Features/Matches/MatchesGetAll.cs kf.api/Features/Matches/MatchGetById.cs \
        kf.api.Tests/EndpointContractTests.cs
git commit -m "feat(api): serve live matches from the game manager"
```

---

### Task 7: The web UI shows who is ready since when

The lobby panel shows each fighter and their state. With the ready timestamp it can also
show how long a fighter has been waiting — which is what the pairing rule orders by.

**Files:**
- Modify: `kf.web_ui/src/entities/lobby/model.ts`
- Modify: `kf.web_ui/src/widgets/lobby/LobbyWidget.tsx`
- Modify: `kf.web_ui/src/widgets/lobby/LobbyWidget.test.tsx`

**Interfaces:**
- Consumes: `GET /api/lobby` now returns `readySince` on every player (Task 3);
  `POST /api/lobby` now returns `matchId` and `opponent` (Task 5).
- Produces:
  ```typescript
  export type LobbyPlayer = {
      name: string;
      isReady: boolean;
      status: 'ready' | 'unready' | string;
      lastActive: string;
      readySince: string | null;   // NEW
  };

  export type LobbyJoinResponse = {
      success: boolean;
      message: string;
      matchId?: string | null;      // NEW
      opponent?: string | null;     // NEW
  };
  ```

- [ ] **Step 1: Write the failing test**

In `kf.web_ui/src/widgets/lobby/LobbyWidget.test.tsx`, replace the `renders fighters with
ready and unready statuses` test with this version — the fixture gains `readySince`, and
the assertion covers the new line:

```typescript
    it('renders fighters with ready and unready statuses', async () => {
        const readySince = new Date().toISOString();
        const mockData: LobbyStatus = {
            waitingCount: 2,
            queue: ['Alice', 'Bob'],
            players: [
                {
                    name: 'Alice',
                    isReady: false,
                    status: 'unready',
                    lastActive: new Date().toISOString(),
                    readySince: null,
                },
                {
                    name: 'Bob',
                    isReady: true,
                    status: 'ready',
                    lastActive: new Date().toISOString(),
                    readySince,
                },
            ],
        };

        getStatus.mockResolvedValue(mockData);
        const { container } = await renderWidget();

        await act(async () => {
            await vi.advanceTimersByTimeAsync(0);
        });

        expect(container.textContent).toContain('Alice');
        expect(container.textContent).toContain('Bob');
        expect(container.textContent).toContain('Unready');
        expect(container.textContent).toContain('Ready');
        expect(container.textContent).toContain('Total in Lobby2');
        expect(container.textContent).toContain('Ready1');
        expect(container.textContent).toContain('Unready1');
        expect(container.textContent).toContain('Ready since');
    });
```

The other two tests in the file use `players: []` and need no change.

- [ ] **Step 2: Run the test to verify it fails**

```bash
cd /Users/feor/projects/students/TOPAI/Practice/knights_fight/kf.web_ui
bun run test
```

Expected: FAIL — `readySince` is missing on the `LobbyPlayer` type, and the widget does
not render "Ready since".

- [ ] **Step 3: Extend the types**

In `kf.web_ui/src/entities/lobby/model.ts`:

```typescript
export type LobbyPlayer = {
    name: string;
    isReady: boolean;
    status: 'ready' | 'unready' | string;
    lastActive: string;
    readySince: string | null;
};

export type LobbyStatus = {
    waitingCount: number;
    queue: string[];
    players: LobbyPlayer[];
};

export type LobbyJoinRequest = {
    challengerName: string;
    isReady?: boolean;
};

export type LobbyJoinResponse = {
    success: boolean;
    message: string;
    matchId?: string | null;
    opponent?: string | null;
};
```

- [ ] **Step 4: Render the ready moment**

In `kf.web_ui/src/widgets/lobby/LobbyWidget.tsx`, replace the `<p className="text-xs text-slate-400">`
block inside `LobbyPlayerRow` with:

```tsx
                <div className="min-w-0">
                    <p className="text-sm font-semibold text-slate-900 truncate">{player.name}</p>
                    <p className="text-xs text-slate-400">
                        Active: {new Date(player.lastActive).toLocaleTimeString()}
                    </p>
                    {player.isReady && player.readySince && (
                        <p className="text-xs text-emerald-600">
                            Ready since {new Date(player.readySince).toLocaleTimeString()}
                        </p>
                    )}
                </div>
```

- [ ] **Step 5: Run the test to verify it passes**

```bash
cd /Users/feor/projects/students/TOPAI/Practice/knights_fight/kf.web_ui
bun run test
```

Expected: PASS.

- [ ] **Step 6: Type-check and lint**

```bash
cd /Users/feor/projects/students/TOPAI/Practice/knights_fight/kf.web_ui
bun run build && bun run lint
```

Expected: both succeed. `bun run build` type-checks first, so a type error fails the build.

- [ ] **Step 7: Commit**

```bash
cd /Users/feor/projects/students/TOPAI/Practice/knights_fight
git add kf.web_ui/src/entities/lobby/model.ts \
        kf.web_ui/src/widgets/lobby/LobbyWidget.tsx \
        kf.web_ui/src/widgets/lobby/LobbyWidget.test.tsx
git commit -m "feat(web): show when a fighter became ready"
```

---

### Task 8: The desktop client and the docs

The desktop client is the only component that can set the ready flag, so it is where a
player sees the match id. The docs are the third source of truth the repo keeps aligned.

**Files:**
- Modify: `kf.client/src/cli/CliApp.cpp`
- Modify: `readme.md`
- Modify: `AGENTS.md`

**Interfaces:**
- Consumes: `POST /api/lobby` now returns `matchId` and `opponent` (Task 5).
- Produces: no new types.

- [ ] **Step 1: Print the match id in the client**

In `kf.client/src/cli/CliApp.cpp`, inside `executeReady`, replace the success branch:

```cpp
        if (r.error.code == cpr::ErrorCode::OK) {
            if (r.status_code >= 200 && r.status_code < 300) {
                std::cout << std::format("   [✓] Success (HTTP {}): {}\n", r.status_code, r.text);

                // A match may have started - the server answers with its id and the opponent.
                try {
                    auto body = nlohmann::json::parse(r.text);
                    if (body.contains("matchId") && !body["matchId"].is_null()) {
                        std::cout << std::format("   [⚔] Match started! id={} opponent={}\n",
                            body["matchId"].get<std::string>(),
                            body.value("opponent", std::string("unknown")));
                    }
                } catch (const nlohmann::json::exception&) {
                    // The response was not JSON - the raw text above already told the story.
                }

                return 0;
            } else {
                std::cerr << std::format("   [!] Request failed with HTTP {}: {}\n", r.status_code, r.text);
                return 1;
            }
        } else {
            std::cerr << std::format("   [!] Failed to connect to lobby endpoint: {}\n", r.error.message);
            return 1;
        }
```

- [ ] **Step 2: Build the client**

```bash
cd /Users/feor/projects/students/TOPAI/Practice/knights_fight/kf.client
cmake -S . -B build -DCMAKE_BUILD_TYPE=Release && cmake --build build -j
```

Expected: the build succeeds. (`nlohmann::json` is already used in this file for the
request payload.)

- [ ] **Step 3: Update `readme.md`**

In `readme.md`, change the two lobby rows of the endpoint table:

```markdown
| POST | `api/lobby` | Join the waiting list; returns the match id when a match starts | **Bearer JWT** |
| GET | `api/lobby` | Lobby status — waiting count and queue | none |
```

and the match rows:

```markdown
| GET | `api/matches` | Live matches | none |
| GET | `api/matches/{id}` | A single live match | none |
```

Fix the **Authorization** section, which is wrong today — `POST /api/lobby` is protected
too, so `[Authorize]` is applied to **two** routes, not one:

```markdown
### Authorization

`[Authorize]` is applied to two routes — `GET /api/test` and `POST /api/lobby`. There is no fallback policy and no global auth filter, so every other endpoint is anonymous. The encrypted POST bodies carry their own protection: the payload is sealed with the server's RSA-OAEP-SHA256 public key, and the plaintext secret is hashed with SHA3-512 before storage.
```

Then, in **Game design**, add a subsection after the battlefield rules:

```markdown
### Matchmaking

- A challenger declares themselves ready with `POST api/lobby`.
- As soon as **two or more** challengers are ready, they are paired and a match starts.
  Four ready challengers start two matches at once.
- The challengers that have been ready the longest are paired first. An odd challenger out
  keeps waiting and is picked up by the next round.
- The pair is taken out of the ready queue as soon as the match starts.
- Matches live in memory on the API instance and are lost when it restarts.
```

Then, in **Known gaps**, replace the first bullet with:

```markdown
- **The UDP game protocol is received but not applied.** `UdpCommandService` authenticates and logs commands; the actual game-state mutation is still a TODO in `CommandProcessor`.
- **Matches are not persisted.** They live in the API's in-memory `GameManager`, so a restart loses them and the leaderboard is not fed by real fights yet.
```

Finally, in **Repository layout**, add the engine to the tree:

```markdown
├── kf.api/               # ASP.NET Core 10 minimal API
│   ├── Features/         # one class per endpoint (Auth, Challenger, Leaderboard, Matches, Lobby, Test)
│   ├── Infrastructure/   # ApiMap, endpoint base classes, crypto, DbContext, UDP services
│   ├── Models/           # entities, requests, responses, mappers
│   └── Migrations/
├── kf.game/              # the game engine - fighters, weapons, bonuses, shrinking board,
│                        # plus GameManager (registry) and Matchmaker (pairing rule)
├── kf.game.Tests/        # xUnit — the engine and the pairing rule
├── kf.api.Tests/         # xUnit — unit tests + EndpointContractTests
```

- [ ] **Step 4: Update `AGENTS.md`**

`AGENTS.md`'s API contract table is already correct — `POST api/lobby` is listed with
`[Authorize]` and the authorization note already names both protected routes. **Leave the
table and the authorization note alone.** Only the **Project layout** list needs the
engine added, because `kf.api` now depends on it:

```markdown
- `kf.api/` — ASP.NET Core 10 minimal API (EF Core + Npgsql, JWT bearer, RSA/AES-GCM crypto, UDP game protocol)
- `kf.game/` — the game engine (fighters, weapons, bonuses, shrinking board) plus `GameManager` and the `Matchmaker`; `kf.api` references it
- `kf.game.Tests/` — xUnit tests for the engine
- `kf.api.Tests/` — xUnit test project (unit tests + `EndpointContractTests.cs`)
```

- [ ] **Step 5: Verify the whole stack**

```bash
cd /Users/feor/projects/students/TOPAI/Practice/knights_fight
dotnet test kf.api.Tests && dotnet test kf.game.Tests
cd kf.web_ui && bun run test && bun run build && bun run lint
```

Expected: all green.

- [ ] **Step 6: Manual end-to-end check**

Start the API and the desktop client, then:

```bash
# Terminal 1 - Alice logs in and becomes ready
./kf.client/build/kf_client ready Alice s3cret true

# Terminal 2 - Bob logs in and becomes ready; this starts the match
./kf.client/build/kf_client ready Bob s3cret true

# The started match is visible
curl -s "http://localhost:8080/api/matches" | jq
```

Expected: the client prints `Match started! id=… opponent=…` for the second player, and
`/api/matches` lists the match with `"status": "InProgress"`.

- [ ] **Step 7: Commit**

```bash
cd /Users/feor/projects/students/TOPAI/Practice/knights_fight
git add kf.client/src/cli/CliApp.cpp readme.md AGENTS.md
git commit -m "docs: describe the lobby matchmaking flow and the client output"
```

---

## Manual verification

```bash
# 1. DB
docker compose -f dev.compose.yaml up -d

# 2. API on 8080
dotnet run --project kf.api/kf.api.csproj --urls http://localhost:8080

# 3. Two challengers become ready -> a match starts
#    (the ready flag needs a Bearer JWT; use the desktop client)
./kf.client/build/kf_client ready Alice s3cret true
./kf.client/build/kf_client ready Bob s3cret true

# 4. The match is live
curl -s "http://localhost:8080/api/matches" | jq
curl -s "http://localhost:8080/api/lobby" | jq

# 5. The UI shows the match in "Current Matches" and the ready timestamps
cd kf.web_ui && bun install && bun run dev
```

## Out of scope

- Routing UDP commands into the game (`CommandProcessor` still logs instead of acting).
- The 0.5-second turn loop.
- Persisting matches to Postgres and feeding `Wins`/`Draws` back into the leaderboard.
- Removing a challenger from the lobby when their match ends.
- The authorization gap in `POST /api/lobby`: the body names the challenger and the handler
  trusts it, so any authenticated caller can flip someone else's ready flag.
