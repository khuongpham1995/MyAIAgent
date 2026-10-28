/**
 * MyAIAgent | Gateway · Presentation
 *
 * Maps document upload, tool discovery, and agent administration inside the authorized admin group.
 */

using MyAIAgent.Common.Application.Models;
using MapsterMapper;
using MediatR;
using MyAIAgent.Gateway.Application.Admin;
using MyAIAgent.Gateway.Application.Admin.Commands.UploadDocument;
using MyAIAgent.Gateway.Application.Admin.Queries.ListMcpTools;
using MyAIAgent.Gateway.Application.Agents;
using MyAIAgent.Gateway.Application.Agents.Commands.CreateAgent;
using MyAIAgent.Gateway.Application.Agents.Commands.DeleteAgent;
using MyAIAgent.Gateway.Application.Agents.Queries.ListAgents;

namespace MyAIAgent.Gateway.Presentation;

public static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/documents", async (IFormFile file, ISender sender, CancellationToken cancellationToken, string? documentId = null) =>
        {
            await using var stream = file.OpenReadStream();
            var command = new UploadDocumentCommand(file.FileName, stream, documentId);
            var result = await sender.Send(command, cancellationToken);
            return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result.ToErrorResponse(StatusCodes.Status400BadRequest));
        }).WithName("UploadDocument")
            .Produces<ResponseModel<DocumentUploadResult>>()
            .Produces<ResponseModel<ErrorResponseDto>>(StatusCodes.Status400BadRequest);

        endpoints.MapGet("/tools", async (ISender sender, CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new ListMcpToolsQuery(), cancellationToken);
            return Results.Ok(result);
        }).WithName("ListTools").Produces<ResponseModel<IReadOnlyList<ToolInfo>>>();

        endpoints.MapGet("/agents", async (ISender sender, CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new ListAgentsQuery(), cancellationToken);
            return Results.Ok(result);
        }).WithName("ListAgents").Produces<ResponseModel<IReadOnlyList<AgentSummary>>>();

        endpoints.MapPost("/agents", async (CreateAgentRequestDto request, ISender sender, IMapper mapper, CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(mapper.Map<CreateAgentCommand>(request), cancellationToken);
            return result.IsSuccess
                ? Results.Created($"/api/admin/agents/{result.Data}", result)
                : Results.BadRequest(result.ToErrorResponse(StatusCodes.Status400BadRequest));
        }).WithName("CreateAgent")
            .Produces<ResponseModel<string>>(StatusCodes.Status201Created)
            .Produces<ResponseModel<ErrorResponseDto>>(StatusCodes.Status400BadRequest);

        endpoints.MapDelete("/agents/{id}", async (string id, ISender sender, CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new DeleteAgentCommand(id), cancellationToken);
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.ToErrorResponse(StatusCodes.Status404NotFound));
        }).WithName("DeleteAgent")
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ResponseModel<ErrorResponseDto>>(StatusCodes.Status404NotFound);

        return endpoints;
    }
}
