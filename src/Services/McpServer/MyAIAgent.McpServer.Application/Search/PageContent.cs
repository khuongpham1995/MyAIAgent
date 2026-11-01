/**
 * MyAIAgent | McpServer · Application
 *
 * Describes bounded web evidence returned to MCP clients, including retrieval time and truncation.
 */

namespace MyAIAgent.McpServer.Application.Search;

public record PageContent(string Url, string Content, string Title, DateTimeOffset RetrievedAtUtc, bool Truncated);
