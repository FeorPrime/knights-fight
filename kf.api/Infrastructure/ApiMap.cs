namespace KnightFight.Api.Infrastructure;

public static partial class ApiMap
{
    // public static readonly string BASE_URL = Environment.GetEnvironmentVariable("DOMAIN") ?? "http://localhost/";

    private const string API_ROOT = "api";
    
    public static class Auth
    {
        private const string RESOURCE_ROOT = $"{API_ROOT}/Auth";
        public const string GET_PUBLIC_KEY = $"{RESOURCE_ROOT}/public-key";
        public const string LOGIN = $"{RESOURCE_ROOT}/login";

    }
    
    public static class Challenger
    {
        private const string RESOURCE_ROOT = $"{API_ROOT}/challenger";
        public const string CREATE = RESOURCE_ROOT;
        public const string GET_ALL = RESOURCE_ROOT;
        public const string STREAM = $"{RESOURCE_ROOT}/stream";
        public const string AVATAR = $"{RESOURCE_ROOT}/{{id}}/avatar";
    }

    public static class Leaderboard
    {
        public const string GET_TOP = $"{API_ROOT}/leaderboard";
    }

    public static class Match
    {
        public const string GET_ALL = $"{API_ROOT}/match";
        public const string GET_BY_ID = $"{API_ROOT}/match/{{id}}";
    }

    public static class Lobby
    {
        public const string JOIN = $"{API_ROOT}/lobby";
        public const string STATUS = $"{API_ROOT}/lobby/status";
    }
}