# UDP Game Command System

## Quick Reference

### Supported Commands
- `move`
- `hit_main`
- `hit_off`
- `turn left`
- `turn right`

Matching is case-insensitive and tolerates surrounding whitespace; the verbs above are
exactly the design-spec commands (`readme.md`) and the only ones accepted — `move up` and
`use hand_left` from an earlier revision are now rejected.

### UDP Packet Format (JSON)
```json
{
  "v": 1,
  "seq": 42,
  "ts": 1700000000000,
  "n": "Player1",
  "otp": "123456",
  "cmd": "hit_main"
}
```

### Service Registration (in Program.cs)
```csharp
builder.Services.AddSingleton<IGameManager>(_ => new GameManager());
builder.Services.AddSingleton<ICommandProcessor, CommandProcessor>();
builder.Services.AddSingleton<IUdpCommandService, UdpCommandService>();
builder.Services.AddHostedService(provider =>
    (UdpCommandService)provider.GetRequiredService<IUdpCommandService>());

// Turn clock — paces the engine's turn loop over live matches
builder.Services.AddSingleton<ITurnService, TurnService>();
builder.Services.AddHostedService(provider =>
    (TurnService)provider.GetRequiredService<ITurnService>());
```

### Configuration (appsettings.json)
```json
{
  "UdpSettings": {
    "Port": 5050
  },
  "TurnSettings": {
    "IntervalMs": 500
  }
}
```

### Lifecycle of one packet
1. `UdpCommandService` receives the datagram on `UdpSettings:Port` and hands it to `CommandParser`.
2. `CommandProcessor` validates the per-challenger TOTP, resolves the live match by
   challenger name (`IGameManager.GetGameByChallenger`) and calls `EnqueueAction` with
   the command's `ToGameAction()`. The queue holds at most `Fighter.MaxBufferedMoves`
   (3) actions — the rest are discarded, as the spec says.
3. `TurnService` dequeues one buffered action per fighter every `TurnSettings:IntervalMs`
   (default 500 ms, floored at 50 ms) through `TickAllActiveGames()`.

### Client Example
```bash
echo '{"v":1,"seq":1,"n":"Player1","otp":"123456","cmd":"hit_main"}' | nc -u localhost 5050
```
