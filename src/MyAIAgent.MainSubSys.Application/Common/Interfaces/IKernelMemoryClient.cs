using MyAIAgent.Common.Domain.Models;
using MyAIAgent.MainSubSys.Application.Admin;

namespace MyAIAgent.MainSubSys.Application.Common.Interfaces;

public interface IKernelMemoryClient
{
    Task<ResponseModel<DocumentUploadResult>> UploadDocumentAsync(
        string fileName,
        Stream content,
        string? documentId,
        CancellationToken cancellationToken);
}
