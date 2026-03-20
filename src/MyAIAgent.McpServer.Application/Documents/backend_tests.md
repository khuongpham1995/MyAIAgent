# Unit Testing Guidelines (xUnit + FluentAssertions)

This document provides best practices and guidelines for writing unit tests in .NET using the xUnit framework and
FluentAssertions library.

## 1. Project Structure

- **Tests** (folder or project)
    - **Api.UnitTests** (project)
    - **Modules.User.UnitTests** (project)
        - **Application** (folder)
        - **Domain** (folder)
        - **Endpoints** (folder)
        - **Infrastructure** (folder)

## 2. Naming Conventions

- **Test Class**: `<ClassUnderTest>Tests`  
  e.g., `CreateUserCommandHandlerTests`
- **Test Methods**: `<MethodUnderTest>_<Scenario>_<ExpectedResult>`  
  e.g., `Handle_GivenValidCommand_ShouldReturnSuccess`

## 3. Arrange-Act-Assert (AAA) Pattern

Structure each test into three clear sections:

```csharp
[Fact]
public async Task Handle_GivenValidCommand_ShouldReturnSuccess()
{
    // Arrange
    var command = new CreateUserCommand { /* ... */ };
    var handler = new CreateUserCommandHandler(/* dependencies */);

    // Act
    var result = await handler.Handle(command, CancellationToken.None);

    // Assert
    result.IsSuccess.Should().BeTrue();
}
