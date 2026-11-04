/**
 * MyAIAgent | KernelMemory · Infrastructure
 *
 * Adds HEIF detection while retaining KernelMemory's other MIME mappings.
 */

using Microsoft.KernelMemory.Pipeline;

namespace MyAIAgent.KernelMemory.Infrastructure.Services;

public sealed class HeifMimeTypeDetection : IMimeTypeDetection
{
    private readonly MimeTypesDetection fallback = new();

    public string GetFileType(string filename) =>
        IsHeif(filename) ? "image/heif" : fallback.GetFileType(filename);

    public bool TryGetFileType(string filename, out string mimeType)
    {
        if (IsHeif(filename))
        {
            mimeType = "image/heif";
            return true;
        }

        var found = fallback.TryGetFileType(filename, out var detected);
        mimeType = detected ?? string.Empty;
        return found;
    }

    private static bool IsHeif(string filename) =>
        string.Equals(Path.GetExtension(filename), ".heif", StringComparison.OrdinalIgnoreCase);
}
