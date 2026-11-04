/**
 * MyAIAgent | KernelMemory · Infrastructure
 *
 * Uses native Office and HTML extraction before OCR fallback where supported.
 */

using System.IO.Compression;
using System.Text;
using System.Xml;
using DocumentFormat.OpenXml.Packaging;
using Microsoft.KernelMemory.DataFormats;
using MyAIAgent.KernelMemory.Infrastructure.Models;

namespace MyAIAgent.KernelMemory.Infrastructure.Services;

public sealed class DocumentIntelligenceOfficeDecoder(
    IDocumentIntelligenceOcrClient client,
    IContentDecoder nativeDecoder,
    DocumentIntelligenceOcrOptions options,
    string mimeType) : IContentDecoder
{
    private const int TextSafetyBudgetBytes = 6_000_000;
    private const string WordMime = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
    private const string ExcelMime = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    private const string PowerPointMime = "application/vnd.openxmlformats-officedocument.presentationml.presentation";

    public bool SupportsMimeType(string candidate) =>
        candidate.StartsWith(mimeType, StringComparison.OrdinalIgnoreCase);

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
        var native = await nativeDecoder.DecodeAsync(data, cancellationToken).ConfigureAwait(false);
        if (native.Sections.Any(section => !string.IsNullOrWhiteSpace(section.Content))) return native;

        int? expectedPages;
        if (mimeType.Equals(WordMime, StringComparison.OrdinalIgnoreCase) ||
            mimeType.Equals("text/html", StringComparison.OrdinalIgnoreCase))
        {
            if (options.Tier.Equals("F0", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Blank Word and HTML documents cannot use OCR fallback on F0.");

            if (mimeType.Equals(WordMime, StringComparison.OrdinalIgnoreCase))
                CheckWordBudget(data, cancellationToken);
            else
                CheckHtmlBudget(data);

            expectedPages = null;
        }
        else
        {
            expectedPages = CountOfficePages(data);
        }

        return await DocumentIntelligenceOcrDecoderSupport.AnalyzeAsync(
            data, expectedPages, client, options, cancellationToken).ConfigureAwait(false);
    }

    private int CountOfficePages(BinaryData data)
    {
        try
        {
            using var stream = data.ToStream();
            if (mimeType.Equals(ExcelMime, StringComparison.OrdinalIgnoreCase))
            {
                using var workbook = SpreadsheetDocument.Open(stream, false);
                return workbook.WorkbookPart?.Workbook?.Sheets?.Elements<DocumentFormat.OpenXml.Spreadsheet.Sheet>().Count()
                    ?? throw new InvalidOperationException("Document worksheet count could not be read.");
            }

            if (mimeType.Equals(PowerPointMime, StringComparison.OrdinalIgnoreCase))
            {
                using var presentation = PresentationDocument.Open(stream, false);
                return presentation.PresentationPart?.Presentation?.SlideIdList?
                    .Elements<DocumentFormat.OpenXml.Presentation.SlideId>().Count()
                    ?? throw new InvalidOperationException("Document slide count could not be read.");
            }

            throw new InvalidOperationException("Document type is not supported for OCR fallback.");
        }
        catch (OpenXmlPackageException exception)
        {
            throw new InvalidOperationException("Document page count could not be read.", exception);
        }
        catch (InvalidDataException exception)
        {
            throw new InvalidOperationException("Document page count could not be read.", exception);
        }
    }

    private static void CheckHtmlBudget(BinaryData data)
    {
        var bytes = data.ToMemory();
        if (bytes.Length >= TextSafetyBudgetBytes)
            throw new InvalidOperationException("Document exceeds the OCR application safety budget.");

        var html = Encoding.UTF8.GetString(bytes.Span);
        if (html.Contains("<!DOCTYPE", StringComparison.OrdinalIgnoreCase) ||
            html.Contains("<!ENTITY", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Document HTML contains unsupported entity declarations.");
    }

    private static void CheckWordBudget(BinaryData data, CancellationToken cancellationToken)
    {
        try
        {
            using var archive = new ZipArchive(data.ToStream(), ZipArchiveMode.Read);
            var hasDocument = false;
            var total = 0;
            foreach (var entry in archive.Entries.Where(entry =>
                         entry.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (entry.FullName.Equals("word/document.xml", StringComparison.OrdinalIgnoreCase))
                    hasDocument = true;

                using var source = entry.Open();
                using var buffer = new MemoryStream();
                var chunk = new byte[8192];
                int read;
                while ((read = source.Read(chunk)) > 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    total += read;
                    if (total >= TextSafetyBudgetBytes)
                        throw new InvalidOperationException("Document exceeds the OCR application safety budget.");
                    buffer.Write(chunk, 0, read);
                }

                buffer.Position = 0;
                using var reader = XmlReader.Create(buffer, new XmlReaderSettings
                {
                    DtdProcessing = DtdProcessing.Prohibit,
                    XmlResolver = null
                });
                while (reader.Read()) cancellationToken.ThrowIfCancellationRequested();
            }

            if (!hasDocument)
                throw new InvalidOperationException("Document Word content could not be read.");
        }
        catch (InvalidDataException exception)
        {
            throw new InvalidOperationException("Document Word content could not be read.", exception);
        }
        catch (XmlException exception)
        {
            throw new InvalidOperationException("Document Word content could not be read.", exception);
        }
    }
}
