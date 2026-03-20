# Backend Project Structure

The modular monolith vertically slices the application into modules that have their own bounded context. Within each
module, the Clean Architecture principle is applied to achieve a separation of technical layers. On top of this, the
application uses the CQRS (Command Query Responsibility Segregation) principle, separating commands (mutating actions)
and queries (retrieving data). To achieve loose coupling between the different components of the application, MediatR (a
mediator-pattern implementation) is used.

## Folder Structure

- **Global** (folder)
- **Api** (project)
- **Workflows** (project)
- **Tests** (folder)
    - **Api.UnitTests** (project)
- **Modules** (folder)
    - **Modules.User.Application** (project)
        - **Behaviors** (folder)
            - `ValidationBehavior.cs` – MediatR pipeline behavior for request validation
        - **User** (folder)
            - **CreateUser** (folder)
                - `CreateUserCommand.cs`
                - `CreateUserCommandHandler.cs`
                - `CreateUserCommandValidator.cs`
                - `UserCreatedDomainEventHandler.cs` – Converts a domain event into an integration event
            - **GetUserById** (folder)
                - `GetUserByIdQuery.cs`
                - `GetUserByIdQueryHandler.cs`
                - `UserResponse.cs`
    - **Modules.User.Domain** (project)
        - **User** (folder)
            - `UserEntity.cs`
        - **Interfaces** (folder)
            - `IUserRepository.cs`
    - **Modules.User.Endpoints** (project)
        - **User** (folder)
            - **CreateUser** (folder)
                - `CreateUserEndpoint.cs`
                - `CreateUserRequest.cs`
                - `CreateUserResponse.cs`
    - **Modules.User.Infrastructure** (project)
        - **Persistence** (folder)
            - `UserRepository.cs`
    - **Tests** (folder)
        - **Modules.{Entity}.UnitTests** (project)
            - **Application** (folder)
            - **Domain** (folder)
            - **Endpoints** (folder)
            - **Infrastructure** (folder)
        - **Modules.{Entity}.IntegrationTests** (project)
- **Common** (folder)  
  Use the structure given above for a module (except the module installer part). Common projects are only referenced by
  their layer counterpart; there is no installation as a common module.

## Dependency Hierarchy

- **Domain** → no references
- **Application** → references Domain
- **Infrastructure** → references Application (and Domain)
- **Endpoints** → references Application (and Domain)

To ensure such a hierarchy, inner layers can define interfaces that are then implemented by the outer layers.
