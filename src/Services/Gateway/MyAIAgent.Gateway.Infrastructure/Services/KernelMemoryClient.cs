/**
 * MyAIAgent | Gateway · Infrastructure
 *
 * Forwards multipart document uploads to KernelMemory and translates transport failures into application results.
 */

using MyAIAgent.Common.Application.Models;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using MyAIAgent.Gateway.Domain.Constants;
using MyAIAgent.Gateway.Application.Common.Interfaces;
using MyAIAgent.Gateway.Application.Admin;

namespace MyAIAgent.Gateway.Infrastructure.Services;

public sealed class KernelMemoryClient(
    HttpClient httpClient,
    ILogger<KernelMemoryClient> logger) : IKernelMemoryClient
{
    public async Task<ResponseModel<DocumentUploadResult>> UploadDocumentAsync(
        string fileName,
        Stream content,
        string? documentId,
        CancellationToken cancellationToken)
    {
        try
        {
            using var form = new MultipartFormDataContent();
            using var fileContent = new StreamContent(content);
            form.Add(fileContent, "file", fileName);

            if (!string.IsNullOrEmpty(documentId))
                form.Add(new StringContent(documentId), "documentId");

            var response = await httpClient.PostAsync("/upload", form, cancellationToken);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<KernelMemoryUploadResult>(
                cancellationToken: cancellationToken);

            var docId = result?.DocumentId ?? documentId ?? Guid.NewGuid().ToString();
            return ResponseModel<DocumentUploadResult>.Succeed(
                new DocumentUploadResult(docId, "accepted"));
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "KernelMemory document upload failed for file {FileName}", fileName);
            return ResponseModel<DocumentUploadResult>.Fail(message: GatewayConstant.Errors.DocumentUploadFailed);
        }
    }

    private sealed record KernelMemoryUploadResult(
        [property: JsonPropertyName("documentId")] string? DocumentId);
}
