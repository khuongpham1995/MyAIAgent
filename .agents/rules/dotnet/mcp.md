---
alwaysApply: true
description: >
  MCP tool registration conventions for MyAIAgent. Covers constants placement,
  tool class structure, MediatR dispatch, and what belongs in each layer.
---

# MCP Tool Registration

## Layer Responsibilities

| Layer | Responsibility |
|---|---|
| Domain | Tool name/description constants in `McpServerConstant.cs` |
| Application | `IRequest<ResponseModel<T>>` command/query + handler + validator |
| Presentation | `[McpServerTool]` method — thin wrapper, dispatches via `ISender` |

## Constants

All tool names, descriptions, and parameter descriptions belong in `McpServerConstant.cs` in the Domain layer. Never hardcode a tool name or description inline in a tool method attribute.

## Tool Class

- `[McpServerToolType]` on the class, `[McpServerTool]` on each method
- Inject `ISender` only — tools never reference infrastructure directly
- Tools are thin: no validation, no mapping, no business logic — dispatch and return
- Return `string` from every tool method — MCP protocol requires serializable output
- Always accept and propagate `CancellationToken`

## Anti-patterns

- Never inject `IApiClient<T>` or any infrastructure type into a tool — always go through MediatR
- Never put validation or business logic in a tool method — it belongs in the handler/validator
- Never hardcode tool names in attributes — always reference `McpServerConstant`
