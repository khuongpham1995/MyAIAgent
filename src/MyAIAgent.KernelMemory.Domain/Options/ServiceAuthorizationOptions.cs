namespace MyAIAgent.KernelMemory.Domain.Options;

public sealed record ServiceAuthorizationOptions
{
    public bool Enabled { get; init; }
    public string AccessKey1 { get; init; } = string.Empty;
    public string AccessKey2 { get; init; } = string.Empty;
}
