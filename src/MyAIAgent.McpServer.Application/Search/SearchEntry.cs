/**
 * MyAIAgent | McpServer · Application
 *
 * Carries a ranked source and its excerpt from the search provider.
 */

namespace MyAIAgent.McpServer.Application.Search;

public record SearchEntry(string? Url, string? Title, string? Content);
