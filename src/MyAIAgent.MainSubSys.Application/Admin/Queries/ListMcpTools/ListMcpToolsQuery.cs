using MediatR;
using MyAIAgent.Common.Domain.Models;
using MyAIAgent.MainSubSys.Application.Interfaces;
using MyAIAgent.MainSubSys.Domain.Models;

namespace MyAIAgent.MainSubSys.Application.Admin.Queries.ListMcpTools;

public sealed record ListMcpToolsQuery : IRequest<ResponseModel<IReadOnlyList<ToolInfo>>>;
