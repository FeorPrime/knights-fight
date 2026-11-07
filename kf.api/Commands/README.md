# UDP Game Command System

## Quick Reference

### Supported Commands
- `move up`
- `move down`
- `move left`
- `move right`
- `use hand_left`
- `use hand_right`
- `turn left`
- `turn right`

### UDP Packet Format (JSON)
```json
{
  "v": 1,
  "seq": 42,
  "ts": 1700000000000,
  "n": "Player1",
  "otp": "123456",
  "cmd": "move up"
}
```

### Service Registration (in Program.cs)
```csharp
builder.Services.AddSingleton<ICommandProcessor, CommandProcessor>();
builder.Services.AddSingleton<IUdpCommandService, UdpCommandService>();
builder.Services.AddHostedService<UdpCommandService>(provider =>
    provider.GetRequiredService<IUdpCommandService>());
```

### Configuration (appsettings.json)
```json
{
  "UdpSettings": {
    "Port": 5050
  }
}
```

### Client Example
```bash
echo '{"v":1,"seq":1,"n":"Player1","otp":"123456","cmd":"move up"}' | nc -u localhost 5050
```
