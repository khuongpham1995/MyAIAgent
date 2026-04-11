namespace MyAIAgent.Common.Domain.Models;

public sealed record ServiceAuthorizationModel
{
    public bool Enabled { get; init; }
    public string AccessKey1 { get; init; } = string.Empty;
    public string AccessKey2 { get; init; } = string.Empty;
    public string HttpHeaderName { get; init; } = "Authorization";
}
