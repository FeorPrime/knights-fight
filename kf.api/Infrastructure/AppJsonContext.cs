using System.Text.Json;
using System.Text.Json.Serialization;
using KnightFight.Api.Models.Requests;
using KnightFight.Api.Models.Responses;

namespace KnightFight.Api.Infrastructure;

[JsonSerializable(typeof(CreateChallengerRequest))]
[JsonSerializable(typeof(CreateChallengerResponse))]
[JsonSerializable(typeof(AuthTokenRequest))]
[JsonSerializable(typeof(ChallengerItemResponse))]
[JsonSerializable(typeof(PagedChallengersResponse))]
[JsonSerializable(typeof(ChallengerNewcomerEvent))]
[JsonSerializable(typeof(GetLeaderboardRequest))]
[JsonSerializable(typeof(RankedChallengerResponse))]
[JsonSerializable(typeof(List<RankedChallengerResponse>))]
[JsonSerializable(typeof(RankedChallengerResponse[]))]
[JsonSerializable(typeof(MatchDto))]
[JsonSerializable(typeof(List<MatchDto>))]
[JsonSerializable(typeof(MatchDto[]))]
[JsonSerializable(typeof(LobbyPlayerDto))]
[JsonSerializable(typeof(List<LobbyPlayerDto>))]
[JsonSerializable(typeof(LobbyPlayerDto[]))]
[JsonSerializable(typeof(LobbyStatusResponse))]
[JsonSerializable(typeof(LobbyJoinRequest))]
[JsonSerializable(typeof(LobbyJoinResponse))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true)]
public partial class AppJsonContext : JsonSerializerContext;