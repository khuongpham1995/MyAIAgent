using FluentValidation;
using MediatR;
using MyAIAgent.Common.Domain.Models;
using MyAIAgent.MainSubSys.Domain.Interfaces;
using MyAIAgent.MainSubSys.Domain.Models;

namespace MyAIAgent.MainSubSys.Application.Handlers.Chat;

public sealed record ChatGatewayCommand(string? SessionId, string UserMessage, string UserId, string? AgentId = null)
    : IRequest<ResponseModel<ChatGatewayResponse>>;

public sealed class ChatGatewayHandler(IOrchestratorClient orchestratorClient)
    : IRequestHandler<ChatGatewayCommand, ResponseModel<ChatGatewayResponse>>
{
    public async Task<ResponseModel<ChatGatewayResponse>> Handle(
        ChatGatewayCommand command,
        CancellationToken cancellationToken) =>
        await orchestratorClient.ChatAsync(
            command.SessionId,
            command.UserMessage,
            command.UserId,
            command.AgentId,
            cancellationToken);
}

public sealed class ChatGatewayCommandValidator : AbstractValidator<ChatGatewayCommand>
{
    public ChatGatewayCommandValidator()
    {
        RuleFor(x => x.UserMessage)
            .NotEmpty().WithMessage("User message is required.")
            .MaximumLength(4000).WithMessage("User message must not exceed 4000 characters.");
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("User identity is required.");
    }
}
