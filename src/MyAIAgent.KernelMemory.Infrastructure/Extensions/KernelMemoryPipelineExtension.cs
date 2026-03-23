using Microsoft.Extensions.DependencyInjection;
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.Pipeline;

namespace MyAIAgent.KernelMemory.Infrastructure.Extensions;

public static class KernelMemoryPipelineExtension
{
    public static int AddHandlersAsHostedServices(
        KernelMemoryConfig config,
        IKernelMemoryBuilder memoryBuilder,
        IServiceCollection services)
    {
        if (!string.Equals(
                config.DataIngestion.OrchestrationType,
                KernelMemoryConfig.OrchestrationTypeDistributed,
                StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        if (!config.Service.RunHandlers) return 0;

        foreach (var handlerConfig in config.Service.Handlers)
        {
            services.AddHandlerAsHostedService(
                config: handlerConfig.Value,
                stepName: handlerConfig.Key);
        }

        return services.Count(s => typeof(IPipelineStepHandler).IsAssignableFrom(s.ServiceType));
    }

    public static int AddHandlersToServerlessMemory(KernelMemoryConfig config, IKernelMemory memory)
    {
        if (memory is not MemoryServerless serverless) return 0;

        foreach (var handlerConfig in config.Service.Handlers)
        {
            serverless.Orchestrator.AddSynchronousHandler(handlerConfig.Value, handlerConfig.Key);
        }

        return serverless.Orchestrator.HandlerNames.Count;
    }
}
