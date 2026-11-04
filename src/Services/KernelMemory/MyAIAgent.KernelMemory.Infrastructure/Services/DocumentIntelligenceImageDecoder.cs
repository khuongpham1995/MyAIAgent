/**
 * MyAIAgent | KernelMemory · Infrastructure
 *
 * Extracts page text from supported images through Azure Document Intelligence.
 */

using Microsoft.KernelMemory.DataFormats;
using MyAIAgent.KernelMemory.Infrastructure.Models;

namespace MyAIAgent.KernelMemory.Infrastructure.Services;

public sealed class DocumentIntelligenceImageDecoder(
    IDocumentIntelligenceOcrClient client,
    DocumentIntelligenceOcrOptions options) : IContentDecoder
{
    public bool SupportsMimeType(string mimeType) =>
        mimeType.StartsWith("image/jpeg", StringComparison.OrdinalIgnoreCase) ||
        mimeType.StartsWith("image/png", StringComparison.OrdinalIgnoreCase) ||
        mimeType.StartsWith("image/tiff", StringComparison.OrdinalIgnoreCase) ||
        mimeType.StartsWith("image/bmp", StringComparison.OrdinalIgnoreCase) ||
        mimeType.StartsWith("image/heif", StringComparison.OrdinalIgnoreCase);

    public async Task<FileContent> DecodeAsync(string filename, CancellationToken cancellationToken = default)
    {
        await using var stream = File.OpenRead(filename);
        return await DecodeAsync(stream, cancellationToken).ConfigureAwait(false);
    }

    public async Task<FileContent> DecodeAsync(Stream data, CancellationToken cancellationToken = default) =>
        await DecodeAsync(await DocumentIntelligenceOcrDecoderSupport.ReadAsync(data, cancellationToken).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);

    public Task<FileContent> DecodeAsync(BinaryData data, CancellationToken cancellationToken = default)
    {
        var bytes = data.ToMemory();
        if (bytes.Length > options.MaxBytes)
            throw new InvalidOperationException("Document exceeds the configured OCR service tier limit.");
        var isTiff = TiffPageCounter.HasHeader(bytes.Span);
        var pages = isTiff ? TiffPageCounter.CountPages(bytes.Span) : 1;

        return DocumentIntelligenceOcrDecoderSupport.AnalyzeAsync(data, pages, client, options, cancellationToken);
    }
}
