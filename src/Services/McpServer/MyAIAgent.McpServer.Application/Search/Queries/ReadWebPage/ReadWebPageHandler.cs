/**
 * MyAIAgent | McpServer · Application
 *
 * Normalizes the requested source and bounds extracted Markdown before exposing it to the model.
 */

using MyAIAgent.Common.Application.Models;
using MediatR;
using MyAIAgent.McpServer.Application.Common.Interfaces;

namespace MyAIAgent.McpServer.Application.Search.Queries.ReadWebPage;

public sealed class ReadWebPageHandler(IWebPageReader reader, TimeProvider timeProvider)
    : IRequestHandler<ReadWebPageQuery, ResponseModel<WebPageContent>>
{
    public async Task<ResponseModel<WebPageContent>> Handle(ReadWebPageQuery request, CancellationToken cancellationToken)
    {
        WebPageUrl.TryNormalize(request.Url, out var url);
        var response = await reader.ReadAsync(url!, cancellationToken);
        if (!response.IsSuccess)
            return ResponseModel<WebPageContent>.Fail(message: response.Message);

        cancellationToken.ThrowIfCancellationRequested();
        var text = response.Data!;
        var length = Math.Min(text.Length, 8000);
        if (length < text.Length && char.IsHighSurrogate(text[length - 1])) length--;
        return ResponseModel<WebPageContent>.Succeed(new(url!.AbsoluteUri, text[..length], timeProvider.GetUtcNow(), length < text.Length));
    }
}
