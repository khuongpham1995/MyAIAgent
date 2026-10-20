/**
 * MyAIAgent | Orchestrator · Application
 *
 * Represents provider failures at the application boundary without exposing SDK-specific exception types.
 */

namespace MyAIAgent.Orchestrator.Application.Common.Exceptions;

// Lets Application react to AI provider failures without referencing a provider SDK.
public sealed class AiProviderException(string message, Exception innerException)
    : Exception(message, innerException);
