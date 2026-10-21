/**
 * MyAIAgent | McpServer · Domain
 *
 * Defines MCP tool names, descriptions, parameter descriptions, and shared search errors.
 */

namespace MyAIAgent.McpServer.Domain.Constants;

public static class McpServerConstant
{
    public const string WebSearchToolName = "web_search";
    public const string WebSearchToolDescription = "Search the web for current or missing information. Returns ranked source URLs, titles, and excerpts; verify claims and cite sources.";
    public const string WebSearchQueryPropertyDescription = "A focused search query, at most 500 characters. Send only the information needed for the lookup.";
    public const string WebSearchTopPropertyDescription = "Maximum number of results, from 1 to 10 (default: 3).";

    public const string GetCurrentDateTimeToolName = "get_current_datetime";
    public const string GetCurrentDateTimeToolDescription = "Returns the current UTC date and time.";

    public const string ErrorSearchFailed = "Web search failed.";
}
