using System.Reflection;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using MyAIAgent.Common.Application.Behaviors;
using MyAIAgent.Common.Application.Extensions;

namespace MyAIAgent.KernelMemory.Application;

public static class IoCExtension
{
    public static void AddApplication(this IServiceCollection services)
    {
        services
            .AddTransientAsMatchingInterfaces(Assembly.GetExecutingAssembly())
            .AddScopedAsMatchingInterfaces(Assembly.GetExecutingAssembly())
            .AddSingletonAsMatchingInterfaces(Assembly.GetExecutingAssembly());

        services.AddMediatR(cfg =>
            {
                cfg.RegisterServicesFromAssemblies(Assembly.GetExecutingAssembly());
            })
            .AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>))
            .AddValidatorsFromAssembly(Assembly.GetExecutingAssembly(), includeInternalTypes: true);
    }
}
