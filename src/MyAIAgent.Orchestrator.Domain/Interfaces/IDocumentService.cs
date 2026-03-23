namespace MyAIAgent.Orchestrator.Domain.Interfaces;

public interface IDocumentService
{
    Task<bool> IsDocumentReadyAsync(string documentId, CancellationToken cancellationToken);
    Task UploadDocumentAsync(string filePath, string documentId, CancellationToken cancellationToken);
    Task<string> AskAsync(string question, CancellationToken cancellationToken);
}
