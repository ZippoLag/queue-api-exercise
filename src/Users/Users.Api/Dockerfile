# syntax=docker/dockerfile:1
#
# Multi-stage production image for the Users API.
#
# The build context MUST be the repository root: the project references
# ../../Shared/QueueApi.Auth and ../../CmsWebhook/CmsWebhook.Domain, outside its
# own directory, and Docker COPY cannot reach outside the context.
#
#   docker build -f src/Users/Users.Api/Dockerfile -t queue-api-users .
#
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Restore layer first: copy only the project files so package restore is cached
# across rebuilds, then the full source tree for the publish step.
COPY global.json Directory.Build.props ./
COPY src/Shared/QueueApi.Auth/QueueApi.Auth.csproj src/Shared/QueueApi.Auth/
COPY src/CmsWebhook/CmsWebhook.Domain/CmsWebhook.Domain.csproj src/CmsWebhook/CmsWebhook.Domain/
COPY src/Users/Users.Application/Users.Application.csproj src/Users/Users.Application/
COPY src/Users/Users.Infrastructure/Users.Infrastructure.csproj src/Users/Users.Infrastructure/
COPY src/Users/Users.Api/Users.Api.csproj src/Users/Users.Api/
RUN dotnet restore src/Users/Users.Api/Users.Api.csproj

COPY src/ src/
RUN dotnet publish src/Users/Users.Api/Users.Api.csproj \
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

ENTRYPOINT ["./Users.Api"]
