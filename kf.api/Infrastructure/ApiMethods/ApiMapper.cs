using System.Reflection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace KnightFight.Api.Infrastructure.ApiMethods;

public static class ApiMapper
{
    public static IServiceCollection RegisterApiEndpoints(this IServiceCollection services)
    {
        var epTypes = Assembly.GetExecutingAssembly().GetTypes()
            .Where(t => t.IsAssignableTo(typeof(IApiMethod)) &&
                        t is { IsClass: true, IsAbstract: false, IsInterface: false });

        var serviceDescriptors = epTypes.Select(type => ServiceDescriptor.Transient(typeof(IApiMethod), type))
            .ToArray();
        
        services.TryAddEnumerable(serviceDescriptors);
        return services;
    }
    
    public static WebApplication MapApiEndpoints(this WebApplication app)
    {
        var eps = app.Services.GetRequiredService<IEnumerable<IApiMethod>>();
        foreach (var ep in eps)
        {
            ep.CheckUriPatternIsSet();
            ep.MapEndpoint(app);
        }

        return app;
    }
}