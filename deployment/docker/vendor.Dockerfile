# syntax=docker/dockerfile:1
#
# Pre-fetches ALL build dependencies (npm packages + NuGet packages + base images)
# and pre-publishes the .NET backend (which needs no build-time args). Build this
# file on a machine WITH internet access; the resulting images are then the only
# inputs `airgapped.Dockerfile` needs — that build makes zero network calls and
# only requires the frontend Entra/AppInsights build args.
#
# This file produces TWO separate images (build one target at a time):
#
#   1. frontend-deps  — node_modules already installed for the current
#                        frontend/package-lock.json
#   2. runtime-base    — the final ASP.NET runtime image with the backend already
#                        published into it (backend publish takes no build args,
#                        so it can be fully finished here)
#
# Build + push both, from the repo root:
#
#   docker build -f deployment/docker/vendor.Dockerfile --target frontend-deps \
#     -t <your-registry>/foundry-agent-frontend-deps:latest .
#   docker push <your-registry>/foundry-agent-frontend-deps:latest
#
#   docker build -f deployment/docker/vendor.Dockerfile --target runtime-base \
#     -t <your-registry>/foundry-agent-runtime-base:latest .
#   docker push <your-registry>/foundry-agent-runtime-base:latest
#
# Transfer both images into the air-gapped environment however your org supports
# (push to a registry mirror reachable from that network, or `docker save`/`load`).
# Re-run this file whenever frontend/package-lock.json, backend source, or the
# base images (node/dotnet) change — otherwise the air-gapped build will use
# stale dependencies.
#
# See deployment/README.md for the full air-gapped build workflow.

ARG DOTNET_SDK_IMAGE=mcr.microsoft.com/dotnet/sdk:10.0
ARG DOTNET_RUNTIME_IMAGE=mcr.microsoft.com/dotnet/aspnet:10.0-alpine
ARG NODE_IMAGE=node:22-alpine

# ============================================================================
# Target: frontend-deps
# node_modules pre-installed; airgapped.Dockerfile builds FROM this image and
# only overlays the actual frontend source + build args on top.
# ============================================================================
FROM ${NODE_IMAGE} AS frontend-deps

WORKDIR /app/frontend

# Only manifest files are needed to resolve dependencies — copying just these
# (instead of full source) keeps this layer cache-friendly and avoids leaking
# unrelated source changes into the vendor image.
COPY frontend/package.json frontend/package-lock.json ./
# .npmrc is optional (custom/internal registry config) — include if present.
COPY frontend/.npmrc* ./

RUN npm ci

# ============================================================================
# Intermediate: backend-build
# Restores + publishes the .NET API. Backend publish takes no build-time ARGs
# (its config is all runtime env vars), so it can be fully finished here.
# ============================================================================
FROM ${DOTNET_SDK_IMAGE} AS backend-build

WORKDIR /app

COPY backend/WebApp.sln ./
COPY backend/WebApp.Api/WebApp.Api.csproj ./backend/WebApp.Api/
COPY backend/WebApp.ServiceDefaults/WebApp.ServiceDefaults.csproj ./backend/WebApp.ServiceDefaults/
RUN dotnet restore backend/WebApp.Api/WebApp.Api.csproj

COPY backend/ ./backend/
RUN dotnet publish backend/WebApp.Api/WebApp.Api.csproj -c Release -o /app/publish

# ============================================================================
# Target: runtime-base
# The final runtime image with the backend already published into it. Only
# missing piece is the frontend's built `dist/` (wwwroot), added by
# airgapped.Dockerfile once the frontend build args are known.
# ============================================================================
FROM ${DOTNET_RUNTIME_IMAGE} AS runtime-base

WORKDIR /app

COPY --from=backend-build /app/publish ./

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

# USER is intentionally NOT set here — airgapped.Dockerfile copies the frontend
# build output in as a later layer and switches to the non-root user last,
# mirroring deployment/docker/frontend.Dockerfile's ordering.
