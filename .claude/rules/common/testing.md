---
alwaysApply: true
description: >
  Enforces testing strategy, naming conventions, and mocking guidelines
  for MyAIAgent using xUnit, Moq, and FluentAssertions.
---

# Testing Rules

## Strategy

- **Unit tests over integration tests** — the project uses mocked dependencies (Moq) rather than real HTTP or database backends.
- **Mock external boundaries only** — mock `IApiClient<T>`, `IKernelMemoryService`, and any third-party interfaces. Do not mock types you own (handlers, validators).
- **Test the handler, not the tool** — MCP tools are thin wrappers; test the MediatR handler and validator logic directly.

## Structure

Mirror the source layout in the test project:

```
MyAIAgent.McpServer.Test/
  Application/
    Handlers/
      Customer/
        CreateCustomerHandlerTests.cs
      Document/
        AskBackendQuestionHandlerTests.cs
    Extensions/
      ApplicationIoCExtensionTests.cs
  Infrastructure/
    ApiClients/
      ApiClientTests.cs
    Services/
      KernelMemoryServiceTests.cs
  McpTools/
    CustomerToolTests.cs
    DocumentToolTests.cs
```

## Naming

`MethodName_Scenario_ExpectedResult`

```
Handle_ValidCreateCustomerCommand_ReturnsSuccess
Handle_CustomerNotFound_ReturnsFailure
ValidateAsync_EmptyName_ReturnsValidationError
```

## Test Pattern — AAA

Arrange / Act / Assert separated by blank lines.

```csharp
[Fact]
public async Task Handle_ValidCommand_ReturnsCreatedCustomer()
{
    // Arrange
    var mockClient = new Mock<IApiClient<Customer>>();
    mockClient.Setup(c => c.PostAsync(It.IsAny<Customer>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(new Customer { Id = Guid.NewGuid(), Name = "Test" });
    var handler = new CreateCustomerHandler(mockClient.Object);
    var command = new CreateCustomerCommand("Test", "123 Main St", "555-0100", "test@example.com");

    // Act
    var result = await handler.Handle(command, CancellationToken.None);

    // Assert
    result.IsSuccess.Should().BeTrue();
    result.Value.Name.Should().Be("Test");
}
```

## FluentAssertions Usage

- Prefer `.Should().Be()`, `.Should().BeTrue()`, `.Should().Contain()` over raw `Assert.*`
- For collections: `.Should().HaveCount()`, `.Should().ContainSingle()`, `.Should().BeEmpty()`
- For async: `.Should().ThrowAsync<T>()` when testing exception paths

## Moq Guidelines

- Use `It.IsAny<T>()` for arguments you don't care about
- Use `It.Is<T>(x => ...)` for precise argument matching
- Always call `mockClient.VerifyAll()` or `mock.Verify(...)` if the call is critical to the test outcome
- Avoid `MockBehavior.Strict` unless you need to assert every call is made
