/**
 * MyAIAgent | McpServer · Application
 *
 * Pairs a fetched page URL with readable content returned by the web-search tool.
 */

namespace MyAIAgent.McpServer.Application.Search;

public record PageContent(string Url, string Content);
