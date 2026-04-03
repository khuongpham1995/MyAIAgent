namespace MyAIAgent.McpServer.Domain.Models;

public record FetchResult(bool Success, string? Content = null, string? ContentType = null, string? Error = null);
