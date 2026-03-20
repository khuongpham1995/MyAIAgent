namespace MyAIAgent.McpServer.Domain.Interfaces;

public interface IKernelMemoryService
{
    Task<string> AskAsync(string question, CancellationToken cancellationToken = default);
    Task<string> UploadDocumentAsync(string filePath, string documentId, CancellationToken cancellationToken = default);
    Task<bool> IsDocumentReadyAsync(string documentId, CancellationToken cancellationToken = default);
}