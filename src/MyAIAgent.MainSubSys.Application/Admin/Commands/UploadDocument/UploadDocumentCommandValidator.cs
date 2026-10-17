using FluentValidation;

namespace MyAIAgent.MainSubSys.Application.Admin.Commands.UploadDocument;

public sealed class UploadDocumentCommandValidator : AbstractValidator<UploadDocumentCommand>
{
    public UploadDocumentCommandValidator()
    {
        RuleFor(x => x.FileName)
            .NotEmpty().WithMessage("File name is required.");

        RuleFor(x => x.Content)
            .NotNull().WithMessage("File content is required.");
    }
}
