namespace MyAIAgent.McpServer.Application.Search;

public record FetchResult(bool Success, string? Content = null, string? ContentType = null, string? Error = null);
