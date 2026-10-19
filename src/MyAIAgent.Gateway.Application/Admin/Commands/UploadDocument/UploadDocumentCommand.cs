using MediatR;
using MyAIAgent.Common.Domain.Models;

namespace MyAIAgent.Gateway.Application.Admin.Commands.UploadDocument;

public sealed record UploadDocumentCommand(string FileName, Stream Content, string? DocumentId)
    : IRequest<ResponseModel<DocumentUploadResult>>;
