/**
 * MyAIAgent | Console Client · Tests
 *
 * Retains outgoing HTTP contract details before requests are disposed by the client.
 */

namespace MyAIAgent.ConsoleClient.Tests;

internal sealed record CapturedRequest(string Method, string Path, string? Body, bool HasAuthorization, string? Accept);
