/**
 * MyAIAgent | Common · Application
 *
 * Converts failed operation results to a typed error envelope without exposing a success payload's default value.
 */
namespace MyAIAgent.Common.Application.Models;

public static class ErrorResponseExtensions
{
    public static ResponseModel<ErrorResponseDto> ToErrorResponse<T>(this ResponseModel<T> response, int statusCode) =>
        ResponseModel<ErrorResponseDto>.Fail(response.Errors, response.Message) with
        {
            Data = new ErrorResponseDto(statusCode),
        };
}
