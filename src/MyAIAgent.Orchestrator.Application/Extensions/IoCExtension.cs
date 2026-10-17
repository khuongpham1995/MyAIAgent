using FluentValidation;
using Mapster;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using MyAIAgent.Common.Application.Behaviors;
using MyAIAgent.Common.Application.Extensions;

namespace MyAIAgent.Orchestrator.Application.Extensions;

public static class IoCExtension
{
    public static void AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(IoCExtension).Assembly;
        var mapsterConfig = new TypeAdapterConfig();
        mapsterConfig.Scan(assembly);
        mapsterConfig.Compile();
        services.AddSingleton(mapsterConfig);
        services.AddMapster();

        services
            .AddTransientAsMatchingInterfaces(assembly)
            .AddScopedAsMatchingInterfaces(assembly)
            .AddSingletonAsMatchingInterfaces(assembly);

        services.AddMediatR(cfg =>
            {
                cfg.RegisterServicesFromAssemblies(assembly);
            })
            .AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>))
            .AddValidatorsFromAssembly(assembly, includeInternalTypes: true);
    }
}
