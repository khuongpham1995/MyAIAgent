/**
 * MyAIAgent | Common · Application
 *
 * Carries the HTTP status in an error payload while message and field errors remain on ResponseModel.
 */

namespace MyAIAgent.Common.Application.Models;

public sealed record ErrorResponseDto(int StatusCode);
