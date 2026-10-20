# Builds and publishes a selected .NET service into the shared runtime image layout.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG SERVICE
WORKDIR /source
COPY . .
RUN test -n "$SERVICE" && dotnet publish "src/MyAIAgent.${SERVICE}.Presentation/MyAIAgent.${SERVICE}.Presentation.csproj" \
    --configuration Release --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0
ARG SERVICE
ENV APP_ASSEMBLY="MyAIAgent.${SERVICE}.Presentation.dll" \
    ASPNETCORE_HTTP_PORTS=8080 \
    ASPNETCORE_ENVIRONMENT=Production
WORKDIR /app
COPY --from=build /app/publish .
USER $APP_UID
ENTRYPOINT ["sh", "-c", "exec dotnet \"$APP_ASSEMBLY\""]
