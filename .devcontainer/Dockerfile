FROM mcr.microsoft.com/devcontainers/dotnet:2-9.0
ENV DEBIAN_FRONTEND=noninteractive

# --- BUILD PHASE (Runs as root to install system tools) ---
USER root

# Install system dependencies. python3 is the full package on purpose: the
# image's default python3-minimal ships no stdlib (no shlex), which breaks
# node-gyp when the AI toolchain compiles native addons (scripts/install-ai-sdlc.sh).
RUN apt-get update && apt-get install -y --no-install-recommends \
    curl unzip procps wget git less iproute2 ca-certificates gnupg \
    openssh-client python3 && \
    apt-get clean && rm -rf /var/lib/apt/lists/*

ENV DEBIAN_FRONTEND=dialog

WORKDIR /workspace

# Update SDK workloads while root owns the shared SDK installation.
RUN dotnet nuget locals all --clear && \
    dotnet workload update && \
    (dotnet workload install wasm-tools-net9 || dotnet workload install wasm-tools || true)

# Extra .NET runtimes required by the VS Code C# extension
# (Roslyn BuildHost needs Microsoft.NETCore.App 8.0; the language server uses .NET 10).
# Runtimes only - the .NET 6 SDK stays the only SDK so `dotnet build` keeps using 6.0.
RUN curl -sSL https://dot.net/v1/dotnet-install.sh | bash /dev/stdin --channel 8.0 --runtime dotnet --install-dir /usr/share/dotnet \
    && curl -sSL https://dot.net/v1/dotnet-install.sh | bash /dev/stdin --channel 8.0 --runtime aspnetcore --install-dir /usr/share/dotnet \
    && curl -sSL https://dot.net/v1/dotnet-install.sh | bash /dev/stdin --channel 10.0 --runtime dotnet --install-dir /usr/share/dotnet \
    && curl -sSL https://dot.net/v1/dotnet-install.sh | bash /dev/stdin --channel 10.0 --runtime aspnetcore --install-dir /usr/share/dotnet

# --- RUNTIME PHASE (Drop privileges) ---
USER vscode

# Install user tools for the same account VS Code uses, and make them available
# in terminals and lifecycle commands.
#ENV PATH="/home/vscode/.dotnet/tools:${PATH}"
RUN dotnet tool install --global dotnet-ef --version 9.*
