namespace KnightFight.Api.Infrastructure.ApiMethods;

public interface IApiMethod
{ 
    public string UriPattern { get; }

    public void CheckUriPatternIsSet()
    {
        if (string.IsNullOrEmpty(UriPattern)) throw new ArgumentNullException(nameof(UriPattern));
    }

    public void MapEndpoint(WebApplication app);
    
}

public abstract class ApiMethod(string uriPattern) : IApiMethod
{
    public required string UriPattern { get; set; } = uriPattern;
    public abstract void MapEndpoint(WebApplication app);
}