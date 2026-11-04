/**
 * MyAIAgent | KernelMemory · Infrastructure
 *
 * Calls the Document Intelligence prebuilt-read model and maps its page text.
 */

using System.Text.Json;
using Azure;
using Azure.AI.DocumentIntelligence;
using Azure.Core;

namespace MyAIAgent.KernelMemory.Infrastructure.Services;

public sealed class DocumentIntelligenceOcrClient(
    DocumentIntelligenceClient client) : IDocumentIntelligenceOcrClient
{
    public async Task<IReadOnlyList<OcrPage>> AnalyzeAsync(BinaryData data, CancellationToken cancellationToken)
    {
        using var content = RequestContent.Create(BinaryData.FromObjectAsJson(new
        {
            base64Source = Convert.ToBase64String(data.ToArray())
        }));
        var context = new RequestContext { CancellationToken = cancellationToken };
        var operation = await client.AnalyzeDocumentAsync(
            WaitUntil.Completed, "prebuilt-read", content,
            stringIndexType: "utf16CodeUnit", context: context).ConfigureAwait(false);

        using var result = JsonDocument.Parse(operation.Value.ToStream());
        var root = result.RootElement;
        if (root.TryGetProperty("analyzeResult", out var analyzeResult))
            root = analyzeResult;
        var allText = root.GetProperty("content").GetString() ?? string.Empty;
        var pages = new List<OcrPage>();
        foreach (var page in root.GetProperty("pages").EnumerateArray())
        {
            var number = page.GetProperty("pageNumber").GetInt32();
            var lines = page.TryGetProperty("lines", out var lineItems) &&
                lineItems.ValueKind == JsonValueKind.Array
                ? lineItems.EnumerateArray()
                    .Select(line => line.GetProperty("content").GetString() ?? string.Empty)
                    .ToList()
                : [];
            if (lines.Count > 0)
            {
                pages.Add(new OcrPage(number, string.Join("\n", lines)));
                continue;
            }

            if (!page.TryGetProperty("spans", out var spans) || spans.ValueKind != JsonValueKind.Array)
            {
                pages.Add(new OcrPage(number, string.Empty));
                continue;
            }

            var fragments = new List<string>();
            foreach (var span in spans.EnumerateArray())
            {
                var offset = span.GetProperty("offset").GetInt32();
                var length = span.GetProperty("length").GetInt32();
                if (offset < 0 || length < 0 || offset > allText.Length || length > allText.Length - offset)
                    throw new InvalidOperationException("Document OCR returned an invalid text span.");
                fragments.Add(allText.Substring(offset, length));
            }
            pages.Add(new OcrPage(number, string.Concat(fragments)));
        }

        return pages;
    }
}
