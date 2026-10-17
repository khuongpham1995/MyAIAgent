using MediatR;
using MyAIAgent.Common.Domain.Models;

namespace MyAIAgent.MainSubSys.Application.Admin.Queries.ListMcpTools;

public sealed record ListMcpToolsQuery : IRequest<ResponseModel<IReadOnlyList<ToolInfo>>>;
