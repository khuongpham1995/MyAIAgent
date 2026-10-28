/**
 * MyAIAgent | Gateway · Application
 *
 * Validates the upload boundary through its pipeline and forwards document content to KernelMemory.
 */

using MyAIAgent.Common.Application.Models;
using MediatR;
using MyAIAgent.Gateway.Application.Common.Interfaces;

namespace MyAIAgent.Gateway.Application.Admin.Commands.UploadDocument;

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
