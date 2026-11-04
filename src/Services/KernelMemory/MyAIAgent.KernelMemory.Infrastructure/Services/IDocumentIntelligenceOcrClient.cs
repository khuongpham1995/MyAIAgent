/**
 * MyAIAgent | KernelMemory · Infrastructure
 *
 * Defines the replaceable boundary for Azure OCR requests.
 */

namespace MyAIAgent.KernelMemory.Infrastructure.Services;

public interface IDocumentIntelligenceOcrClient
{
    Task<IReadOnlyList<OcrPage>> AnalyzeAsync(BinaryData data, CancellationToken cancellationToken);
}
