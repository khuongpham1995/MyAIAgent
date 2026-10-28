/**
 * MyAIAgent | McpServer · Application
 *
 * Returns a bounded webpage extract with its source, retrieval time, and truncation status.
 */

namespace MyAIAgent.McpServer.Application.Search;

public sealed record WebPageContent(string Url, string Content, DateTimeOffset RetrievedAtUtc, bool Truncated);
