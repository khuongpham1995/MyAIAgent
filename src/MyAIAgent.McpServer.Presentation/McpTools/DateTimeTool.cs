using ModelContextProtocol.Server;
using MyAIAgent.McpServer.Domain.Constants;
using System.ComponentModel;

namespace MyAIAgent.McpServer.Presentation.McpTools;

[McpServerToolType]
public sealed class DateTimeTool(TimeProvider timeProvider)
{
    [McpServerTool(Name = McpServerConstant.GetCurrentDateTimeToolName),
     Description(McpServerConstant.GetCurrentDateTimeToolDescription)]
    public string GetCurrentDateTime() => timeProvider.GetUtcNow().ToString("O");
}
