using FluentValidation;
using MyAIAgent.McpServer.Domain.Interfaces;
using MediatR;

namespace MyAIAgent.McpServer.Application.Handlers.Document;

public enum Document
{
    BackendStructure,
    BackendTests
}

public record AskBackendQuestionCommand(string Question, Document Type = Document.BackendStructure) : IRequest<string>;

public class AskBackendQuestionHandler(IKernelMemoryService kernelMemoryService)
    : IRequestHandler<AskBackendQuestionCommand, string>
{
    private const string BackendDocument = "backend_structure.md";
    private const string TestDocument = "backend_tests.md";

    public async Task<string> Handle(AskBackendQuestionCommand request, CancellationToken cancellationToken)
    {
        var document = request.Type == Document.BackendStructure ? BackendDocument : TestDocument;
        var filePath = Path.Combine(AppContext.BaseDirectory, "Documents", document);
        if (!File.Exists(filePath)) throw new FileNotFoundException($"File not found: {filePath}");
        var documentId = document.Replace(".md", string.Empty).ToLowerInvariant();

        while (!await kernelMemoryService.IsDocumentReadyAsync(documentId, cancellationToken))
        {
            await Task.Delay(2000, cancellationToken);
            await kernelMemoryService.UploadDocumentAsync(filePath, documentId, cancellationToken);
        }

        return await kernelMemoryService.AskAsync(GetPrompt(request.Question, request.Type), cancellationToken);
    }

    private static string GetPrompt(string question, Document type)
    {
        var documentName = type == Document.BackendStructure ? "Backend Structure" : "Backend Tests";
        return @$"
            You are a highly skilled developer assistant specializing in {documentName} for software applications. Your expertise lies in analyzing documentation and generating code snippets that adhere to best practices and optimize functionality.
            Your task is to review the provided backend structure and unit test structure documents to generate the appropriate code. Here are the details of the documents you need to analyze: 
            # Input:
            Question: '{question}'
            # Task:
            Based on the provided question and the content of the document, generate the necessary code snippets or functions that address the question.
            The generated code should be well-commented, follow standard coding conventions. 
            Please ensure the code is following the content of document.
            Be wary of the following constraints:
            Ensure compatibility with the specified programming language (C#) and framework (ASP.NET Core).
            # Output
            1. **Plan** (numbered list)
            2. **Code Snippets** in fenced blocks with file paths
        ";
    }
}

public class AskQuestionCommandValidator : AbstractValidator<AskBackendQuestionCommand>
{
    public AskQuestionCommandValidator()
    {
        RuleFor(x => x.Question)
            .NotEmpty()
            .WithMessage("Question is required.");
    }
}