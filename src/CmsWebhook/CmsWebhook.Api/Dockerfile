# syntax=docker/dockerfile:1
#
# Multi-stage production image for the CmsWebhook API.
#
# The build context MUST be the repository root: the project references
# ../Shared/QueueApi.Auth, outside its own directory, and Docker COPY cannot
# reach outside the context.
#
#   docker build -f src/CmsWebhook/CmsWebhook.Api/Dockerfile -t queue-api-cms .
#
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Restore layer first: copy only the project files so package restore is cached
# across rebuilds, then the full source tree for the publish step.
COPY global.json Directory.Build.props ./
COPY src/Shared/QueueApi.Auth/QueueApi.Auth.csproj src/Shared/QueueApi.Auth/
COPY src/CmsWebhook/CmsWebhook.Domain/CmsWebhook.Domain.csproj src/CmsWebhook/CmsWebhook.Domain/
COPY src/CmsWebhook/CmsWebhook.Application/CmsWebhook.Application.csproj src/CmsWebhook/CmsWebhook.Application/
COPY src/CmsWebhook/CmsWebhook.Infrastructure/CmsWebhook.Infrastructure.csproj src/CmsWebhook/CmsWebhook.Infrastructure/
COPY src/CmsWebhook/CmsWebhook.Api/CmsWebhook.Api.csproj src/CmsWebhook/CmsWebhook.Api/
RUN dotnet restore src/CmsWebhook/CmsWebhook.Api/CmsWebhook.Api.csproj

COPY src/ src/
RUN dotnet publish src/CmsWebhook/CmsWebhook.Api/CmsWebhook.Api.csproj \
    -c Release --no-restore -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# Bind all interfaces: the container is reached via a published host port (or a
# load balancer target). The default Kestrel bind (localhost) is loopback-only,
# which would make the API unreachable from outside the container.
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
EXPOSE 8080

# The aspnet base image provides an unprivileged 'app' user.
USER app

ENTRYPOINT ["./CmsWebhook.Api"]
