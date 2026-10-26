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
| Application | Request, handler, and validator for application operations; web search uses a typed `ResponseModel` result, serialized by the tool |
| Presentation | `[McpServerTool]` methods dispatch application operations via `ISender` |

## Constants

All tool names, descriptions, and parameter descriptions belong in `McpServerConstant.cs` in the Domain layer. Never hardcode a tool name or description inline in a tool method attribute.

## Tool Class

- `[McpServerToolType]` on the class, `[McpServerTool]` on each method
- Dispatch application operations through `ISender`. Tools never reference infrastructure directly.
- Keep application validation, mapping, and business logic in handlers and validators.
- This project returns `string` from its tool methods: serialized search evidence or a bounded webpage extract.
- Tool methods that perform asynchronous work or I/O accept and propagate `CancellationToken`.

## Anti-patterns

- Never inject `IApiClient<T>` or any infrastructure type into a tool. Dispatch application operations through MediatR.
- Never put validation or business logic in a tool method — it belongs in the handler/validator
- Never hardcode tool names in attributes — always reference `McpServerConstant`
