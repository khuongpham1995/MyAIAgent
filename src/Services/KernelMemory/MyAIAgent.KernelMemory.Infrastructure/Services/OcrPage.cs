/**
 * MyAIAgent | KernelMemory · Infrastructure
 *
 * Holds text and the source page number returned by OCR.
 */

namespace MyAIAgent.KernelMemory.Infrastructure.Services;

public sealed record OcrPage(int PageNumber, string Text);
