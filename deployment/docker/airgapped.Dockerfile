# syntax=docker/dockerfile:1
#
# Builds the foundry-agent-webapp image entirely offline, using the pre-built
# vendor images produced by vendor.Dockerfile (frontend-deps + runtime-base).
# No `npm ci`, no `dotnet restore`, no `dotnet publish`, and no base-image pulls
# happen here — everything needed is already baked into those two images.
#
# The ONLY things this build needs are the frontend build args (Entra config +
# optional Application Insights connection string) — same args accepted by
# deployment/docker/frontend.Dockerfile.
#
# Prerequisite: FRONTEND_DEPS_IMAGE and RUNTIME_BASE_IMAGE must already exist in
# a registry (or local Docker daemon) reachable from the air-gapped environment.
# See vendor.Dockerfile for how to produce and transfer them.
#
# Build (fully offline):
#
#   docker build -f deployment/docker/airgapped.Dockerfile \
#     --build-arg FRONTEND_DEPS_IMAGE=<your-registry>/foundry-agent-frontend-deps:latest \
#     --build-arg RUNTIME_BASE_IMAGE=<your-registry>/foundry-agent-runtime-base:latest \
#     --build-arg ENTRA_SPA_CLIENT_ID=<client-id> \
#     --build-arg ENTRA_TENANT_ID=<tenant-id> \
#     --build-arg ENTRA_AUTHORITY=<authority-host> \
#     --build-arg ENTRA_BACKEND_CLIENT_ID=<backend-client-id> \
#     --build-arg ENTRA_API_SCOPE=<api-scope> \
#     --build-arg APPLICATIONINSIGHTS_FRONTEND_CONNECTION_STRING=<conn-string> \
#     -t web:latest .

ARG FRONTEND_DEPS_IMAGE=foundry-agent-frontend-deps:latest
ARG RUNTIME_BASE_IMAGE=foundry-agent-runtime-base:latest

# ============================================================================
# Stage 1: Build the frontend using pre-installed node_modules (no npm ci)
# ============================================================================
FROM ${FRONTEND_DEPS_IMAGE} AS frontend-build

# Build arguments for environment variables (required at build time) — same
# set accepted by deployment/docker/frontend.Dockerfile.
ARG ENTRA_SPA_CLIENT_ID
ARG ENTRA_TENANT_ID
ARG ENTRA_AUTHORITY="https://login.microsoftonline.com"
ARG ENTRA_BACKEND_CLIENT_ID=""
ARG ENTRA_API_SCOPE=""
ARG APPLICATIONINSIGHTS_FRONTEND_CONNECTION_STRING=""

WORKDIR /app/frontend

# Overlay the actual frontend source on top of the node_modules already
# installed in FRONTEND_DEPS_IMAGE. node_modules is untouched by this COPY
# because it isn't part of the build context (see .dockerignore).
COPY frontend/ ./

# Remove ANY local environment files to prevent localhost config from being used
RUN rm -f .env.local .env.development .env

ENV NODE_ENV=production
ENV VITE_ENTRA_SPA_CLIENT_ID=$ENTRA_SPA_CLIENT_ID
ENV VITE_ENTRA_TENANT_ID=$ENTRA_TENANT_ID
ENV VITE_ENTRA_AUTHORITY=$ENTRA_AUTHORITY
ENV VITE_ENTRA_BACKEND_CLIENT_ID=$ENTRA_BACKEND_CLIENT_ID
ENV VITE_ENTRA_API_SCOPE=$ENTRA_API_SCOPE
ENV VITE_APPLICATIONINSIGHTS_CONNECTION_STRING=$APPLICATIONINSIGHTS_FRONTEND_CONNECTION_STRING
# Don't set VITE_API_URL - will default to "/api" (same origin)

# No install step — dependencies already present from FRONTEND_DEPS_IMAGE
RUN npm run build

# ============================================================================
# Stage 2: Runtime — RUNTIME_BASE_IMAGE already has the published backend;
# only the frontend's built assets are added here.
# ============================================================================
FROM ${RUNTIME_BASE_IMAGE}

WORKDIR /app

# Copy built React frontend into wwwroot (ASP.NET static files directory)
COPY --from=frontend-build /app/frontend/dist ./wwwroot

# Run as non-root user (built-in 'app' user in ASP.NET alpine images)
USER app

# Start the .NET API (which will also serve frontend static files from wwwroot)
ENTRYPOINT ["dotnet", "WebApp.Api.dll"]
