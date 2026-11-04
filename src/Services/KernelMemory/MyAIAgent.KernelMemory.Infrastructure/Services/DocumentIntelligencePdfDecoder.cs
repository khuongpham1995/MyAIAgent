/**
 * MyAIAgent | KernelMemory · Infrastructure
 *
 * Uses native PDF extraction before invoking OCR for textless documents.
 */

using Microsoft.KernelMemory.DataFormats;
using MyAIAgent.KernelMemory.Infrastructure.Models;

namespace MyAIAgent.KernelMemory.Infrastructure.Services;

public sealed class DocumentIntelligencePdfDecoder(
    IDocumentIntelligenceOcrClient client,
    IContentDecoder pdfDecoder,
    DocumentIntelligenceOcrOptions options) : IContentDecoder
{
    public bool SupportsMimeType(string mimeType) =>
        mimeType.StartsWith("application/pdf", StringComparison.OrdinalIgnoreCase);

    public async Task<FileContent> DecodeAsync(string filename, CancellationToken cancellationToken = default)
    {
        await using var stream = File.OpenRead(filename);
        return await DecodeAsync(stream, cancellationToken).ConfigureAwait(false);
    }

    public async Task<FileContent> DecodeAsync(Stream data, CancellationToken cancellationToken = default) =>
        await DecodeAsync(await DocumentIntelligenceOcrDecoderSupport.ReadAsync(data, cancellationToken).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);

    public async Task<FileContent> DecodeAsync(BinaryData data, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var native = await pdfDecoder.DecodeAsync(data, cancellationToken).ConfigureAwait(false);
        if (native.Sections.Any(section => !string.IsNullOrWhiteSpace(section.Content))) return native;

        return await DocumentIntelligenceOcrDecoderSupport.AnalyzeAsync(
            data, native.Sections.Count, client, options, cancellationToken).ConfigureAwait(false);
    }
}
