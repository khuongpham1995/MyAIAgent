using System.Text.Json;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using MyAIAgent.McpServer.Application.Interfaces;
using MyAIAgent.McpServer.Domain.Models;

namespace MyAIAgent.McpServer.Application.Search.Queries.WebSearch;

public record WebSearchQuery(string Query, int Top = 3) : IRequest<string>;
