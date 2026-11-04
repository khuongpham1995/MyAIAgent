/**
 * MyAIAgent | KernelMemory · Infrastructure
 *
 * Applies shared OCR limits and converts validated pages to KernelMemory content.
 */

using Azure;
using Microsoft.KernelMemory.DataFormats;
using MyAIAgent.KernelMemory.Infrastructure.Models;

namespace MyAIAgent.KernelMemory.Infrastructure.Services;

internal static class DocumentIntelligenceOcrDecoderSupport
{
    internal static async Task<FileContent> AnalyzeAsync(
        BinaryData data,
        int? expectedPages,
        IDocumentIntelligenceOcrClient client,
        DocumentIntelligenceOcrOptions options,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (data.ToMemory().Length > options.MaxBytes || expectedPages is < 1 || expectedPages > options.MaxPages)
            throw new InvalidOperationException("Document exceeds the configured OCR service tier limit.");

        IReadOnlyList<OcrPage> pages;
        try
        {
            pages = await client.AnalyzeAsync(data, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException exception)
        {
            throw new InvalidOperationException("Document OCR timed out.", exception);
        }
        catch (RequestFailedException exception)
        {
            throw new InvalidOperationException("Document OCR failed.", exception);
        }
        catch (HttpRequestException exception)
        {
            throw new InvalidOperationException("Document OCR failed.", exception);
        }

        if (pages.Count == 0 || pages.Count > options.MaxPages ||
            (expectedPages.HasValue && pages.Count != expectedPages.Value) ||
            pages.Select(page => page.PageNumber).Order().Where((number, index) => number != index + 1).Any() ||
            pages.All(page => string.IsNullOrWhiteSpace(page.Text)))
            throw new InvalidOperationException("Document OCR returned incomplete or empty content.");

        var content = new FileContent("text/plain");
        foreach (var page in pages.OrderBy(page => page.PageNumber))
            content.Sections.Add(new Chunk(page.Text, page.PageNumber));
        return content;
    }

    internal static async Task<BinaryData> ReadAsync(Stream stream, CancellationToken cancellationToken) =>
        await BinaryData.FromStreamAsync(stream, cancellationToken).ConfigureAwait(false);
}
