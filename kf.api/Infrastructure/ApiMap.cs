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
        public const string GET_ALL = $"{API_ROOT}/challengers";
        public const string GET_ALL_LEGACY = RESOURCE_ROOT;
        public const string STREAM = $"{API_ROOT}/challengers/stream";
        public const string AVATAR = $"{API_ROOT}/challengers/{{id}}/avatar";
    }

    public static class Leaderboard
    {
        public const string GET_TOP = $"{API_ROOT}/leaderboard";
    }

    public static class Match
    {
        public const string GET_ALL = $"{API_ROOT}/matches";
        public const string GET_BY_ID = $"{API_ROOT}/matches/{{id}}";
    }

    public static class Lobby
    {
        public const string JOIN = $"{API_ROOT}/lobby";
        public const string STATUS = $"{API_ROOT}/lobby/status";
    }
}