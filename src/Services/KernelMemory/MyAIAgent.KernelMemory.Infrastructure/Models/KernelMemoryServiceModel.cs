/**
 * MyAIAgent | KernelMemory · Infrastructure
 *
 * Represents web-service enablement and upload-size options for KernelMemory infrastructure configuration.
 */

namespace MyAIAgent.KernelMemory.Infrastructure.Models;

public sealed record KernelMemoryServiceModel
{
    public bool RunWebService { get; init; } = true;
    public double? MaxUploadSizeMb { get; init; }
}
