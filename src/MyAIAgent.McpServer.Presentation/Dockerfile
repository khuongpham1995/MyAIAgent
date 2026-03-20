FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["McpServer.Presentation/McpServer.Presentation.csproj", "McpServer.Presentation/"]
COPY ["McpServer.Application/McpServer.Application.csproj", "McpServer.Application/"]
COPY ["McpServer.Domain/McpServer.Domain.csproj", "McpServer.Domain/"]
COPY ["McpServer.Infrastructure/McpServer.Infrastructure.csproj", "McpServer.Infrastructure/"]
RUN dotnet restore "McpServer.Presentation/McpServer.Presentation.csproj"
COPY . .
WORKDIR "/src/McpServer.Presentation"
RUN dotnet build "McpServer.Presentation.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "McpServer.Presentation.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "McpServer.Presentation.dll"]
