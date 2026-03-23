namespace MyAIAgent.KernelMemory.Domain.Options;

public sealed record KernelMemoryServiceOptions
{
    public bool RunWebService { get; init; } = true;
    public double? MaxUploadSizeMb { get; init; }
}
