/**
 * MyAIAgent | Gateway · Application
 *
 * Carries document upload input through the Gateway MediatR application boundary.
 */

using MyAIAgent.Common.Application.Models;
using MediatR;

namespace MyAIAgent.Gateway.Application.Admin.Commands.UploadDocument;

public sealed record UploadDocumentCommand(string FileName, Stream Content, string? DocumentId)
    : IRequest<ResponseModel<DocumentUploadResult>>;
