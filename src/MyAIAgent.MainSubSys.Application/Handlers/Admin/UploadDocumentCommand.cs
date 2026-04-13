using FluentValidation;
using MediatR;
using MyAIAgent.Common.Domain.Models;
using MyAIAgent.MainSubSys.Domain.Interfaces;
using MyAIAgent.MainSubSys.Domain.Models;

namespace MyAIAgent.MainSubSys.Application.Handlers.Admin;

public sealed record UploadDocumentCommand(string FileName, Stream Content, string? DocumentId)
    : IRequest<ResponseModel<DocumentUploadResult>>;

public sealed class UploadDocumentHandler(IKernelMemoryClient kernelMemoryClient)
    : IRequestHandler<UploadDocumentCommand, ResponseModel<DocumentUploadResult>>
{
    public async Task<ResponseModel<DocumentUploadResult>> Handle(
        UploadDocumentCommand command,
        CancellationToken cancellationToken) =>
        await kernelMemoryClient.UploadDocumentAsync(
            command.FileName,
            command.Content,
            command.DocumentId,
            cancellationToken);
}

public sealed class UploadDocumentCommandValidator : AbstractValidator<UploadDocumentCommand>
{
    public UploadDocumentCommandValidator()
    {
        RuleFor(x => x.FileName)
            .NotEmpty().WithMessage("File name is required.");

        RuleFor(x => x.Content)
            .NotNull().WithMessage("File content is required.");
    }
}
