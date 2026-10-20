/**
 * MyAIAgent | Gateway · Application
 *
 * Defines the Gateway application port for forwarding document uploads to the KernelMemory daemon.
 */

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
