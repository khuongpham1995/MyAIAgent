using MyAIAgent.Common.Domain.Models;
using MyAIAgent.MainSubSys.Domain.Models;

namespace MyAIAgent.MainSubSys.Application.Interfaces;

public interface IKernelMemoryClient
{
    Task<ResponseModel<DocumentUploadResult>> UploadDocumentAsync(
        string fileName,
        Stream content,
        string? documentId,
        CancellationToken cancellationToken);
}
