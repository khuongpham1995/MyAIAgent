/**
 * MyAIAgent | KernelMemory · Infrastructure
 *
 * Defines the Azure Document Intelligence OCR settings and tier limits.
 */

namespace MyAIAgent.KernelMemory.Infrastructure.Models;

public sealed class DocumentIntelligenceOcrOptions
{
    public bool Enabled { get; init; }
    public string Tier { get; init; } = "F0";
    public string Endpoint { get; init; } = string.Empty;
    public string Auth { get; init; } = string.Empty;
    public string ApiKey { get; init; } = string.Empty;
    public string ClientId { get; init; } = string.Empty;

    public long MaxBytes => string.Equals(Tier, "S0", StringComparison.OrdinalIgnoreCase)
        ? 500L * 1024 * 1024 : 4L * 1024 * 1024;

    public int MaxPages => string.Equals(Tier, "S0", StringComparison.OrdinalIgnoreCase) ? 2000 : 2;
}
