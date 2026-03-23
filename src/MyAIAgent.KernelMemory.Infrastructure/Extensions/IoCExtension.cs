using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MyAIAgent.Common.Application.Extensions;
using MyAIAgent.Common.Infrastructure.Extensions;
using MyAIAgent.KernelMemory.Domain.Options;
using MyAIAgent.KernelMemory.Infrastructure.Data;

namespace MyAIAgent.KernelMemory.Infrastructure.Extensions;

public static class IoCExtension
{
    public static void AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddTransientAsMatchingInterfaces(typeof(IoCExtension).Assembly)
            .AddScopedAsMatchingInterfaces(typeof(IoCExtension).Assembly)
            .AddSingletonAsMatchingInterfaces(typeof(IoCExtension).Assembly);

        services.AddCosmosDb<KernelMemoryDbContext>(configuration);

        services.Configure<KernelMemoryServiceOptions>(
            configuration.GetSection("KernelMemory:Service"));

        services.Configure<ServiceAuthorizationOptions>(
            configuration.GetSection("KernelMemory:ServiceAuthorization"));

        var maxUploadMb = configuration.GetValue<double?>("KernelMemory:Service:MaxUploadSizeMb");
        if (!maxUploadMb.HasValue) return;
        var maxBytes = (long)(maxUploadMb.Value * 1024 * 1024);
        services.Configure<KestrelServerOptions>(x => { x.Limits.MaxRequestBodySize = maxBytes; });
        services.Configure<FormOptions>(x =>
        {
            x.MultipartBodyLengthLimit = maxBytes;
            x.ValueLengthLimit = int.MaxValue;
        });
    }
}
