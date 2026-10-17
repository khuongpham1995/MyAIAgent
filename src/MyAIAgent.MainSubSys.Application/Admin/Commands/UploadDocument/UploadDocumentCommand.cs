using MediatR;
using MyAIAgent.Common.Domain.Models;

namespace MyAIAgent.MainSubSys.Application.Admin.Commands.UploadDocument;

public sealed record UploadDocumentCommand(string FileName, Stream Content, string? DocumentId)
    : IRequest<ResponseModel<DocumentUploadResult>>;
