/**
 * MyAIAgent | Console Client
 *
 * Carries safe client-facing failures without exposing raw HTTP bodies or exception details.
 */

namespace MyAIAgent.ConsoleClient;

internal sealed class GatewayClientException(string message) : Exception(message);
