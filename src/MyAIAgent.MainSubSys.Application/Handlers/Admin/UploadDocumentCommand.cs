using FluentValidation;
using MediatR;
using MyAIAgent.Common.Domain.Models;
using MyAIAgent.MainSubSys.Application.Interfaces;
using MyAIAgent.MainSubSys.Domain.Models;

namespace MyAIAgent.MainSubSys.Application.Handlers.Admin;

public sealed record UploadDocumentCommand(string FileName, Stream Content, string? DocumentId)
    : IRequest<ResponseModel<DocumentUploadResult>>;
