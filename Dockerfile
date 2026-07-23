# FF-Occam MCP — Multi-stage build
# Runtime: .NET 10 AOT + Node.js 20 + Playwright Chromium

# ---- Stage 1: Build AOT binary ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
# Native AOT needs the clang toolchain + zlib headers for the native link step.
RUN apt-get update && \
    apt-get install -y --no-install-recommends clang zlib1g-dev && \
    rm -rf /var/lib/apt/lists/*
WORKDIR /src

# Copy project files first (layer caching). RID-specific restore for AOT.
COPY Directory.Build.props .
COPY src/FFOccamMcp.Core/FFOccamMcp.Core.csproj src/FFOccamMcp.Core/
RUN dotnet restore src/FFOccamMcp.Core -r linux-x64

# Copy source and publish
COPY src/FFOccamMcp.Core/ src/FFOccamMcp.Core/
RUN dotnet publish src/FFOccamMcp.Core \
    -c Release \
    -r linux-x64 \
    --self-contained \
    -o /app/publish \
    /p:PublishAot=true

# ---- Stage 2: Node.js workers ----
# workers/ is a single npm workspace (members: http-extract, browser-extract,
# css-extract). Install from the one root lockfile, not per-package.
FROM node:20-slim AS workers
WORKDIR /app/workers
ENV PLAYWRIGHT_SKIP_BROWSER_DOWNLOAD=1
COPY workers/ ./
RUN npm ci --omit=dev

# ---- Stage 3: Runtime ----
FROM mcr.microsoft.com/dotnet/runtime-deps:10.0 AS runtime

# Install Node.js 20
RUN apt-get update && \
    apt-get install -y curl gnupg && \
    curl -fsSL https://deb.nodesource.com/setup_20.x | bash - && \
    apt-get install -y nodejs && \
    rm -rf /var/lib/apt/lists/*

WORKDIR /app

# Environment
ENV OCCAM_HOME=/app
ENV PATH="/app:${PATH}"
# Shared, world-readable browser path so the non-root user can use Chromium.
ENV PLAYWRIGHT_BROWSERS_PATH=/ms-playwright

# Copy AOT binary
COPY --from=build /app/publish/OccamMcp.Core /app/occam

# Copy workers
COPY --from=workers /app/workers /app/workers

# Copy scripts
COPY scripts/ /app/scripts/

# Playwright installs its own OS dependencies (distro-aware, avoids hand-maintaining
# apt package names across base-image changes) plus Chromium, as root.
RUN cd /app/workers/browser-extract && \
    npx playwright install-deps chromium && \
    npx playwright install chromium && \
    rm -rf /var/lib/apt/lists/*

# Create non-root user; own the app and the shared browser cache.
RUN groupadd -r occam && useradd -r -g occam -d /app occam && \
    chown -R occam:occam /app /ms-playwright
USER occam

# Health check
HEALTHCHECK --interval=30s --timeout=5s --retries=3 \
    CMD /app/occam --version || exit 1

# Default: stdio MCP server
ENTRYPOINT ["/app/occam"]
