FROM mcr.microsoft.com/devcontainers/dotnet:2-9.0
ENV DEBIAN_FRONTEND=noninteractive

# --- BUILD PHASE (Runs as root to install system tools) ---
USER root

# 1. Install system dependencies
RUN apt-get update && apt-get install -y --no-install-recommends \
    curl unzip procps wget git less iproute2 ca-certificates gnupg \
    openssh-client && \
    apt-get clean && rm -rf /var/lib/apt/lists/*

ENV DEBIAN_FRONTEND=dialog

WORKDIR /workspace

# 2. Install .NET tools and workloads as the unprivileged user
RUN dotnet tool install --global dotnet-ef && \
    dotnet nuget locals all --clear && \
    dotnet workload update && \
    (dotnet workload install wasm-tools-net9 || dotnet workload install wasm-tools || true)
    
# --- RUNTIME PHASE (Drop privileges) ---
USER vscode

# Keep container alive without needing root services like SSH
ENTRYPOINT ["tail", "-f", "/dev/null"]