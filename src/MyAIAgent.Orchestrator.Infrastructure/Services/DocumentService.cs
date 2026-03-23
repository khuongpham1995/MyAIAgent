using Microsoft.KernelMemory;
using MyAIAgent.Common.Domain.ServiceLifetime;
using MyAIAgent.Orchestrator.Domain.Interfaces;

namespace MyAIAgent.Orchestrator.Infrastructure.Services;

public sealed class DocumentService(IKernelMemory kernelMemory)
    : IDocumentService, IScoped
{
    public async Task<bool> IsDocumentReadyAsync(string documentId, CancellationToken cancellationToken)
    {
        var status = await kernelMemory.GetDocumentStatusAsync(documentId, cancellationToken: cancellationToken);
        return status?.Completed ?? false;
    }

    public async Task UploadDocumentAsync(string filePath, string documentId, CancellationToken cancellationToken)
    {
        await kernelMemory.ImportDocumentAsync(filePath, documentId: documentId, cancellationToken: cancellationToken);
    }

    public async Task<string> AskAsync(string question, CancellationToken cancellationToken)
    {
        var answer = await kernelMemory.AskAsync(question, cancellationToken: cancellationToken);
        return answer.Result;
    }
}
