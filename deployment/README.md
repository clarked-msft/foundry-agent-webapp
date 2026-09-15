# Deployment Directory

**AI Assistance**: See `.github/skills/deploying-to-azure/SKILL.md` for deployment patterns.

## Structure

```
deployment/
├── docker/              # Docker build files
│   ├── frontend.Dockerfile  # Single-container build (React + ASP.NET Core)
│   ├── vendor.Dockerfile    # Air-gapped: pre-fetches deps, run with internet access
│   └── airgapped.Dockerfile # Air-gapped: builds offline from vendor.Dockerfile's output
├── app-service/          # Manual App Service configuration guide (alternative to Container Apps)
├── hooks/                # Azure Developer CLI lifecycle hooks
│   ├── preprovision.ps1     # Create Entra app + discover AI Foundry + generate config
│   ├── postprovision.ps1    # Update Entra redirect URIs + assign RBAC
│   ├── predeploy.ps1        # Build container (local Docker or ACR cloud build)
│   ├── postdown.ps1         # Cleanup (optional)
│   └── modules/             # Reusable PowerShell modules
│       ├── Get-AIFoundryAgents.ps1
│       └── New-EntraAppRegistration.ps1
└── scripts/             # User-invoked scripts
    └── start-local-dev.ps1  # Start native local development
```

## Build Strategy

Container builds use **local Docker when available** with **ACR cloud build as fallback**:

| Docker Installed | Build Method | Speed |
|------------------|--------------|-------|
| ✅ Yes, running | Local Docker build + push to ACR | ~2 min |
| ❌ No | ACR cloud build | ~4-5 min |

This is handled automatically by `predeploy.ps1`.

## Key Commands

| Command | Purpose | When to Use |
|---------|---------|-------------|
| `azd up` | Full provision + deploy | Initial setup, infrastructure changes |
| `azd deploy` | Code-only deployment | Fast iteration on code changes |
| `azd down` | Tear down resources | Cleanup |

## Hook Workflow

```
azd up
  ├─ preprovision.ps1 (Entra + AI Foundry discovery + .env generation)
  ├─ provision (Bicep deployment with placeholder image)
  ├─ postprovision.ps1 (updates Entra redirect URIs + RBAC)
  └─ predeploy.ps1 (builds container - local Docker or ACR cloud)

azd deploy
  └─ predeploy.ps1 (builds + pushes + updates Container App)
```

## Quick Reference

**Common tasks**:
- First deployment: `azd up`
- Deploy code changes: `azd deploy`
- Local development: `.\deployment\scripts\start-local-dev.ps1`
- Clean up: `azd down --force --purge`

## Docker Details

**Build strategy**: Multi-stage (React build → .NET build → Runtime)

**Build args**: Client ID and Tenant ID are automatically passed to the Dockerfile from azd environment variables.

**Custom npm registries**: Add `.npmrc` to `frontend/` directory - automatically copied during build

## Air-Gapped / Offline Builds

`frontend.Dockerfile` requires internet access at build time (npm registry, NuGet feeds, and
base image pulls). For environments without internet access, use the two-file split instead:

1. **`vendor.Dockerfile`** — build this once, on a machine WITH internet access. It produces two
   images:
   - `frontend-deps` — `npm ci` already run (node_modules pre-installed)
   - `runtime-base` — the .NET backend already restored + published (backend publish takes no
     build-time args, so it can be fully finished here) layered on top of the final runtime
     base image

   ```bash
   docker build -f deployment/docker/vendor.Dockerfile --target frontend-deps \
     -t foundry-agent-frontend-deps:latest .
   docker build -f deployment/docker/vendor.Dockerfile --target runtime-base \
     -t foundry-agent-runtime-base:latest .
   docker save foundry-agent-frontend-deps:latest | gzip > foundry-agent-frontend-deps-latest.tar.gz
   docker save foundry-agent-runtime-base:latest | gzip > foundry-agent-runtime-base-latest.tar.gz
   ```

   Transfer both images into the air-gapped environment (push to a registry mirror reachable
   from that network, or `docker save`/`docker load`). Re-run this step whenever
   `frontend/package-lock.json`, backend source, or the base images change.

2. **`airgapped.Dockerfile`** — run this inside the air-gapped environment. It builds FROM the
   two vendor images only, makes **zero network calls**, and needs only the frontend build args:

   ```bash
   docker build -f deployment/docker/airgapped.Dockerfile \
     --build-arg FRONTEND_DEPS_IMAGE=<registry>/foundry-agent-frontend-deps:latest \
     --build-arg RUNTIME_BASE_IMAGE=<registry>/foundry-agent-runtime-base:latest \
     --build-arg ENTRA_SPA_CLIENT_ID=<client-id> \
     --build-arg ENTRA_TENANT_ID=<tenant-id> \
     --build-arg ENTRA_AUTHORITY=<authority-host> \
     --build-arg ENTRA_BACKEND_CLIENT_ID=<backend-client-id> \
     --build-arg ENTRA_API_SCOPE=<api-scope> \
     -t web:latest .
   ```

   This produces the same runtime image as `frontend.Dockerfile` — same runtime env vars
   (`AI_AGENT_ENDPOINT`, `AI_SCOPE`, `ENTRA_TENANT_ID`, etc.), same `/api/health` endpoint.

For AI-assisted development, see `.github/skills/deploying-to-azure/SKILL.md`.
