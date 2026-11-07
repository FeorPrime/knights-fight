namespace KnightFight.Api.Models.Responses;

public record LobbyPlayerDto(
    string Name,
    bool IsReady,
    string Status,
    DateTime LastActive,
    DateTime? ReadySince,
    string? AvatarUrl = null
);

public record LobbyStatusResponse(
    int WaitingCount,
    IReadOnlyList<string> Queue,
    IReadOnlyList<LobbyPlayerDto> Players
);

public record LobbyJoinRequest(
    string ChallengerName,
    bool IsReady = true
);

public record LobbyJoinResponse(
    bool Success,
    string Message,
    Guid? MatchId = null,
    string? Opponent = null
);
