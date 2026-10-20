/**
 * MyAIAgent | McpServer · Application
 *
 * Carries a search-provider result link and snippet before page content is fetched.
 */

namespace MyAIAgent.McpServer.Application.Search;

public record SearchEntry(string? Link, string? Snippet);
