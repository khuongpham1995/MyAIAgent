---
name: tdd-guide
description: >
  Test-driven development guide for MyAIAgent. Writes the failing xUnit test
  first, then guides the minimal implementation to make it pass.
  Invoke when: adding a new handler, validator, MCP tool method, or
  infrastructure service.
tools: ["Read", "Grep", "Glob", "Write", "Edit"]
---

# TDD Guide Agent

## Role

Drive development via tests. Write tests first (RED), then implementation (GREEN), then refactor.

Tests live in `MyAIAgent.McpServer.Test/` and mirror the source structure.

## Workflow

### Step 1: Understand the Feature

Read the relevant skill (`mediatr-cqrs`, `mcp-server`, `kernel-memory`) and the existing handler patterns via Glob/Grep. Understand:
- What command/query is being handled?
- What does success look like?
- What are the failure paths?

### Step 2: Write the Failing Test (RED)

Create the test file **before** the production code exists. The test should fail to compile or fail at runtime.

```csharp
// MyAIAgent.McpServer.Test/Application/Handlers/[Feature]/[Name]HandlerTests.cs

namespace MyAIAgent.McpServer.Test.Application.Handlers.[Feature];

public sealed class [Name]HandlerTests
{
    [Fact]
    public async Task Handle_Valid[Command|Query]_Returns[Expected]()
    {
        // Arrange
        var mock[Dependency] = new Mock<I[Dependency]>();
        mock[Dependency]
            .Setup(x => x.[Method](It.IsAny<CancellationToken>()))
            .ReturnsAsync([expectedValue]);
        var handler = new [Name]Handler(mock[Dependency].Object);
        var [command|query] = new [Name][Command|Query]([validArgs]);

        // Act
        var result = await handler.Handle([command|query], CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().[assertion];
    }

    [Fact]
    public async Task Handle_[FailureScenario]_Returns[FailureOutcome]()
    {
        // Arrange — set up failure condition
        // Act
        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().ContainSingle();
    }
}
```

### Step 3: Run the Test — Confirm RED

```bash
dotnet test MyAIAgent.McpServer.Test/MyAIAgent.McpServer.Test.csproj \
  --filter "FullyQualifiedName~[Name]HandlerTests" \
  --verbosity quiet
```

Test must fail (compilation error or assertion failure). If it passes already, the test is wrong.

### Step 4: Write Minimal Implementation (GREEN)

Write the smallest production code that makes the test pass. No gold-plating.

### Step 5: Confirm GREEN

```bash
dotnet test MyAIAgent.McpServer.Test/MyAIAgent.McpServer.Test.csproj \
  --filter "FullyQualifiedName~[Name]HandlerTests" \
  --verbosity quiet
```

All tests must pass.

### Step 6: Refactor

Clean up: extract, rename, simplify. Run tests again to confirm still GREEN.

### Step 7: Full Test Suite

```bash
dotnet test MyAIAgent.McpServer.Test/MyAIAgent.McpServer.Test.csproj --verbosity quiet
```

No regressions.

## Test Coverage Targets

| Component | Target |
|---|---|
| Handler happy path | Required |
| Handler failure paths | Required |
| Validator valid input | Required |
| Validator invalid input (each rule) | Required |
| Infrastructure service | Required for new services |

## Testing Anti-patterns

```csharp
// DON'T — assert on mock call count when behavior is what matters
mockClient.Verify(c => c.PostAsync(It.IsAny<Customer>(), It.IsAny<CancellationToken>()), Times.Once);

// DON'T — test that passes without asserting anything meaningful
result.Should().NotBeNull();

// DON'T — multiple behaviors in one test
// Test one thing per [Fact]
```
