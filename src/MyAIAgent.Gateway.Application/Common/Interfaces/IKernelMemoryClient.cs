using MyAIAgent.Common.Domain.Models;
using MyAIAgent.Gateway.Application.Admin;

namespace MyAIAgent.Gateway.Application.Common.Interfaces;

public interface IKernelMemoryClient
{
    Task<ResponseModel<DocumentUploadResult>> UploadDocumentAsync(
        string fileName,
        Stream content,
        string? documentId,
        CancellationToken cancellationToken);
}
