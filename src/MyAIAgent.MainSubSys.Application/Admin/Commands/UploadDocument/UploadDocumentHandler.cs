using MediatR;
using MyAIAgent.Common.Domain.Models;
using MyAIAgent.MainSubSys.Application.Common.Interfaces;

namespace MyAIAgent.MainSubSys.Application.Admin.Commands.UploadDocument;

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
