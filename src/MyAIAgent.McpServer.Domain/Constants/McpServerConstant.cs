namespace MyAIAgent.McpServer.Domain.Constants;

public static class McpServerConstant
{
    public const string WebSearchToolName = "web_search";
    public const string WebSearchToolDescription = "Search the web using Google and return scraped page content for the top results.";
    public const string WebSearchQueryPropertyDescription = "The search query to look up online.";
    public const string WebSearchTopPropertyDescription = "Maximum number of results to fetch (default: 3).";

    public const string GetCurrentDateTimeToolName = "get_current_datetime";
    public const string GetCurrentDateTimeToolDescription = "Returns the current UTC date and time.";

    public const string ErrorSearchFailed = "Web search failed.";
}
