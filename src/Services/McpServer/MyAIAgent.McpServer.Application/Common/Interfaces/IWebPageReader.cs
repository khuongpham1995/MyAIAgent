/**
 * MyAIAgent | McpServer · Application
 *
 * Retrieves Markdown from a validated public webpage through the configured extraction provider.
 */

using MyAIAgent.Common.Application.Models;
namespace MyAIAgent.McpServer.Application.Common.Interfaces;

public interface IWebPageReader
{
    Task<ResponseModel<string>> ReadAsync(Uri url, CancellationToken cancellationToken);
}
