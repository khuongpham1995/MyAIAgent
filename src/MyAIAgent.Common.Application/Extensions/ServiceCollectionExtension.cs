/**
 * MyAIAgent | Common · Application
 *
 * Registers implementations by lifetime marker through Scrutor while excluding the marker interfaces themselves.
 */

using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using MyAIAgent.Common.Domain.Abstractions;
using Scrutor;

namespace MyAIAgent.Common.Application.Extensions;

public static class ServiceCollectionExtension
{
    public static IServiceCollection AddTransientAsMatchingInterfaces(
        this IServiceCollection services, Assembly assembly) =>
        services.Scan(scan =>
            scan.FromAssemblies(assembly)
                .AddClasses(filter => filter.AssignableTo<ITransient>(), false)
                .UsingRegistrationStrategy(RegistrationStrategy.Throw)
                .AsImplementedInterfaces(type => type.Name != nameof(ITransient))
                .WithTransientLifetime());

    public static IServiceCollection AddScopedAsMatchingInterfaces(
        this IServiceCollection services, Assembly assembly) =>
        services.Scan(scan =>
            scan.FromAssemblies(assembly)
                .AddClasses(filter => filter.AssignableTo<IScoped>(), false)
                .UsingRegistrationStrategy(RegistrationStrategy.Throw)
                .AsImplementedInterfaces(type => type.Name != nameof(IScoped))
                .WithScopedLifetime());

    public static IServiceCollection AddSingletonAsMatchingInterfaces(
        this IServiceCollection services, Assembly assembly) =>
        services.Scan(scan =>
            scan.FromAssemblies(assembly)
                .AddClasses(filter => filter.AssignableTo<ISingleton>(), false)
                .UsingRegistrationStrategy(RegistrationStrategy.Throw)
                .AsImplementedInterfaces(type => type.Name != nameof(ISingleton))
                .WithSingletonLifetime());
}
