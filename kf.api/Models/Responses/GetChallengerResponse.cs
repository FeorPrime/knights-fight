namespace KnightFight.Api.Models.Responses;

public class GetChallengerResponse
{
    public required string ChallengerName { get; init; }
    public int Challenges { get; init; } = 0;
    public int Wins { get; init; } = 0;
    public int Loses { get; init; } = 0;
    public int Draws { get; init; } = 0;
}