/**
 * MyAIAgent | Gateway · Application
 *
 * Carries the accepted document identifier and upload status back through the Gateway application boundary.
 */

namespace MyAIAgent.Gateway.Application.Admin;

public sealed record DocumentUploadResult(string DocumentId, string Status);
