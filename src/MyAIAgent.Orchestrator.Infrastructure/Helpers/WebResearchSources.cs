/**
 * MyAIAgent | Orchestrator · Infrastructure
 *
 * Keeps one turn's bounded web-tool budget and validated source links independently of model-written citations.
 */

using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;
using MyAIAgent.Common.Infrastructure.Http;

namespace MyAIAgent.Orchestrator.Infrastructure.Helpers;

public sealed class WebResearchSources
{
    private const int MaximumResultCharacters = 1024 * 1024;
    private readonly object sync = new();
    private readonly HashSet<string> urls = new(StringComparer.Ordinal);
    private int invocations;

    public static bool IsWebTool(string name) => name is "web_search" or "read_webpage";

    public bool TryBeginInvocation() => Interlocked.Increment(ref invocations) <= 3;

    public void Record(string name, object? result)
    {
        if (result is TextContent text) RecordText(name, text.Text);
        else if (result is string value) RecordText(name, value);
        else if (result is JsonElement json)
        {
            if (json.ValueKind == JsonValueKind.String) RecordText(name, json.GetString());
            else if (json.ValueKind == JsonValueKind.Object
                && (!json.TryGetProperty("isError", out var error) || error.ValueKind == JsonValueKind.False)
                && json.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.Array)
            {
                foreach (var block in content.EnumerateArray())
                    if (block.ValueKind == JsonValueKind.Object
                        && block.TryGetProperty("type", out var type) && type.ValueKind == JsonValueKind.String && type.GetString() == "text"
                        && block.TryGetProperty("text", out var blockText) && blockText.ValueKind == JsonValueKind.String)
                        RecordText(name, blockText.GetString());
            }
        }
    }

    public string Footer()
    {
        lock (sync)
        {
            if (urls.Count == 0) return string.Empty;
            var footer = new StringBuilder("\n\n**Retrieved sources**\n");
            foreach (var url in urls)
            {
                var link = "- <" + url.Replace("<", "%3C").Replace(">", "%3E") + ">\n";
                if (footer.Length + link.Length > 8000) break;
                footer.Append(link);
            }
            return footer.ToString();
        }
    }

    private void RecordText(string name, string? text)
    {
        if (!IsWebTool(name) || string.IsNullOrWhiteSpace(text) || text.Length > MaximumResultCharacters) return;
        try
        {
            using var document = JsonDocument.Parse(text, new JsonDocumentOptions { MaxDepth = 16 });
            var root = document.RootElement;
            if (name == "web_search" && root.ValueKind == JsonValueKind.Array)
            {
                foreach (var entry in root.EnumerateArray()) RecordEntry(entry);
            }
            else if (name == "read_webpage") RecordEntry(root);
        }
        catch (JsonException)
        {
            // A malformed tool response may still be shown to the model, but cannot establish source links.
        }
    }

    private void RecordEntry(JsonElement entry)
    {
        if (entry.ValueKind != JsonValueKind.Object
            || !entry.TryGetProperty("Content", out var content) || content.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(content.GetString())
            || !entry.TryGetProperty("Url", out var source) || source.ValueKind != JsonValueKind.String)
            return;
        var value = source.GetString();
        if (value is null || value.Length > 2048 || value.Any(char.IsWhiteSpace) || value.Any(char.IsControl) || value.Contains('\\')
            || !Uri.TryCreate(value, UriKind.Absolute, out var uri) || !uri.IsDefaultPort
            || uri.Query.Length != 0 || !OutboundAddressPolicy.IsPublicUri(uri))
            return;
        var host = uri.IdnHost.TrimEnd('.');
        if (!host.Contains('.') || new[] { "local", "internal", "home", "lan", "test", "invalid" }
            .Any(suffix => host.EndsWith('.' + suffix, StringComparison.OrdinalIgnoreCase))) return;
        lock (sync)
        {
            if (urls.Count < 10) urls.Add(uri.AbsoluteUri);
        }
    }
}
