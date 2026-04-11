namespace MyAIAgent.KernelMemory.Domain.Models;

public sealed record KernelMemoryServiceModel
{
    public bool RunWebService { get; init; } = true;
    public double? MaxUploadSizeMb { get; init; }
}
