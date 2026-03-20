using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using MyAIAgent.Common.ServiceLifetime;
using Scrutor;

namespace MyAIAgent.Common.Extensions;

public static class ServiceCollectionExtension
{
    /// <summary>
    /// Adds all the implementations of <see cref="ITransient"/> inside the specified assembly as transient.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <param name="assembly">The assembly to scan for transient services.</param>
    /// <returns>The same service collection so that multiple calls can be chained.</returns>
    public static IServiceCollection AddTransientAsMatchingInterfaces(this IServiceCollection services, Assembly assembly) =>
        services.Scan(scan =>
            scan.FromAssemblies(assembly)
                .AddClasses(filter => filter.AssignableTo<ITransient>(), false)
                .UsingRegistrationStrategy(RegistrationStrategy.Throw)
                .AsImplementedInterfaces(type => type.Name != nameof(ITransient))
                .WithTransientLifetime());

    /// <summary>
    /// Adds all the implementations of <see cref="IScoped"/> inside the specified assembly as scoped.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <param name="assembly">The assembly to scan for scoped services.</param>
    /// <returns>The same service collection so that multiple calls can be chained.</returns>
    public static IServiceCollection AddScopedAsMatchingInterfaces(this IServiceCollection services, Assembly assembly)
    {
        return services.Scan(scan =>
            scan.FromAssemblies(assembly)
                .AddClasses(filter => filter.AssignableTo<IScoped>(), false)
                .UsingRegistrationStrategy(RegistrationStrategy.Throw)
                .AsImplementedInterfaces(type => type.Name != nameof(IScoped))
                .WithScopedLifetime());
    }

    /// <summary>
    /// Adds all the implementations of <see cref="ISingleton"/> inside the specified assembly as scoped.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <param name="assembly">The assembly to scan for singleton services.</param>
    /// <returns>The same service collection so that multiple calls can be chained.</returns>
    public static IServiceCollection AddSingletonAsMatchingInterfaces(this IServiceCollection services, Assembly assembly) =>
        services.Scan(scan =>
            scan.FromAssemblies(assembly)
                .AddClasses(filter => filter.AssignableTo<ISingleton>(), false)
                .UsingRegistrationStrategy(RegistrationStrategy.Throw)
                .AsImplementedInterfaces(type => type.Name != nameof(ISingleton))
                .WithSingletonLifetime());
}