# Running this app on Azure App Service (manual, no Bicep automation)

This repository's official deployment path is **Azure Container Apps** via `azd up`
(see [`infra/README.md`](../../infra/README.md) and [`deployment/README.md`](../README.md)).
There is **no App Service Bicep module in this repo** — this document describes how to run the
same container image (`deployment/docker/frontend.Dockerfile`) on **Azure App Service for
Containers (Linux)** manually, and what changes from the Container Apps path.

> The image itself is portable — it's a standard Linux container listening on port 8080 that
> serves the API and the built React app from `wwwroot`. What's *not* portable automatically is
> the identity/RBAC/redirect-URI wiring, which today is written specifically for Container Apps
> resources (`WEB_IDENTITY_PRINCIPAL_ID`, Container App FQDN, etc. in `deployment/hooks/postprovision.ps1`).

## What stays the same

- Build the image with `docker build -f deployment/docker/frontend.Dockerfile .` — same build args as documented in [`deployment/README.md`](../README.md).
- The container still requires the same runtime configuration values (env vars) described in
  [`.github/skills/troubleshooting-authentication/SKILL.md`](../../.github/skills/troubleshooting-authentication/SKILL.md)
  and [`infra/main-app.bicep`](../../infra/main-app.bicep).
- Health check path is `/api/health` (unauthenticated).

## What's different on App Service

| Concern | Container Apps (this repo's default) | App Service (manual) |
|---|---|---|
| Identity | User-assigned managed identity, output as `WEB_IDENTITY_PRINCIPAL_ID` | Enable a **user-assigned managed identity** on the Web App (`az webapp identity assign`) |
| Config injection | Container Apps env vars (`infra/main-app.bicep`) | **App Settings** (`az webapp config appsettings set`) |
| ACR pull | MI + `AcrPull` role, no admin credentials | Configure App Service's container settings to pull via MI, or ACR admin creds (less secure) |
| Redirect URIs | Container App FQDN, set by `postprovision.ps1` after provision | Your App Service default hostname (`https://<app-name>.azurewebsites.net`) or custom domain |
| OBO FIC subject | Container App's MI principal ID | The **App Service's MI principal ID** — the FIC subject must be updated to match |
| Scale-to-zero | Native (Container Apps) | Not available on Basic/Standard tiers; requires manual scaling or accepting always-on cost |
| Infra automation | `infra/*.bicep` + `azd up` | None provided — steps below are manual `az` commands |

## Step-by-step

### 1. Create the Web App (Linux, container)

```bash
az group create -n rg-my-app -l eastus2

az appservice plan create \
  -g rg-my-app -n asp-my-app \
  --is-linux --sku B1

az webapp create \
  -g rg-my-app -n my-foundry-agent-app \
  --plan asp-my-app \
  --deployment-container-image-name mcr.microsoft.com/k8se/quickstart:latest
```

### 2. Enable a user-assigned managed identity

```bash
az identity create -g rg-my-app -n id-my-app

az webapp identity assign \
  -g rg-my-app -n my-foundry-agent-app \
  --identities /subscriptions/<sub>/resourceGroups/rg-my-app/providers/Microsoft.ManagedIdentity/userAssignedIdentities/id-my-app
```

Grant this identity `AcrPull` on your ACR, and the same AI Foundry RBAC roles the Container Apps
path assigns in `postprovision.ps1`:

```bash
az role assignment create --assignee <identity-principal-id> --role AcrPull --scope <acr-resource-id>

for role in "Cognitive Services User" "Cognitive Services OpenAI Contributor" "Azure AI Developer"; do
  az role assignment create --assignee <identity-principal-id> --role "$role" --scope <ai-foundry-resource-id>
done
```

### 3. Point App Service at the ACR image using the managed identity

```bash
az webapp config container set \
  -g rg-my-app -n my-foundry-agent-app \
  --container-image-name <acr-name>.azurecr.io/web:latest \
  --container-registry-user "" \
  --container-registry-password ""

az resource update \
  --ids $(az webapp show -g rg-my-app -n my-foundry-agent-app --query id -o tsv) \
  --set properties.siteConfig.acrUseManagedIdentityCreds=true \
         properties.siteConfig.acrUserManagedIdentityID=<identity-client-id>
```

### 4. Set App Settings (equivalent of Container Apps env vars)

These map 1:1 to the env vars injected in [`infra/main-app.bicep`](../../infra/main-app.bicep):

```bash
az webapp config appsettings set -g rg-my-app -n my-foundry-agent-app --settings \
  ASPNETCORE_ENVIRONMENT=Production \
  ASPNETCORE_URLS=http://+:8080 \
  ENTRA_SPA_CLIENT_ID=<spa-client-id> \
  ENTRA_TENANT_ID=<tenant-id> \
  ENTRA_AUTHORITY=https://login.microsoftonline.com \
  AI_AGENT_ENDPOINT=<ai-foundry-project-endpoint> \
  AI_AGENT_ID=<agent-id> \
  AI_SCOPE=https://ai.azure.com/.default \
  MANAGED_IDENTITY_CLIENT_ID=<identity-client-id>
```

Add `ENTRA_BACKEND_CLIENT_ID` only if you're using OBO (see step 6).

> App Service injects these as environment variables at container startup, same as Container
> Apps — no code changes needed. `WEBSITES_PORT=8080` must also be set so App Service proxies
> to the container's actual listening port.

```bash
az webapp config appsettings set -g rg-my-app -n my-foundry-agent-app --settings WEBSITES_PORT=8080
```

### 5. Configure the health check path

```bash
az webapp config set -g rg-my-app -n my-foundry-agent-app --generic-configurations '{"healthCheckPath": "/api/health"}'
```

### 6. Update Entra app registration for the App Service hostname

The SPA app's redirect URIs and (if OBO is enabled) the backend app's Federated Identity
Credential subject must point at App Service instead of the Container App FQDN. This replaces
what `deployment/hooks/postprovision.ps1` does for Container Apps:

```bash
appServiceUrl="https://$(az webapp show -g rg-my-app -n my-foundry-agent-app --query defaultHostName -o tsv)"

az rest --method PATCH \
  --uri "https://graph.microsoft.com/v1.0/applications/<spa-app-object-id>" \
  --headers "Content-Type=application/json" \
  --body "{\"spa\":{\"redirectUris\":[\"http://localhost:5173\",\"http://localhost:8080\",\"$appServiceUrl\"]}}"
```

If OBO is enabled, re-create the FIC with the App Service's managed identity principal ID as the
subject (the FIC created by `postprovision.ps1` targets the Container App's MI and will not work
for App Service):

```bash
az ad app federated-credential create --id <backend-app-object-id> --parameters '{
  "name": "app-service-mi-fic",
  "issuer": "https://login.microsoftonline.com/<tenant-id>/v2.0",
  "subject": "<app-service-identity-principal-id>",
  "audiences": ["api://AzureADTokenExchange"]
}'
```

### 7. Build and push the image

Use the same Dockerfile and build args as Container Apps — see
[`deployment/README.md`](../README.md) and [`deployment/hooks/predeploy.ps1`](../hooks/predeploy.ps1)
for the exact `--build-arg` list (`ENTRA_SPA_CLIENT_ID`, `ENTRA_TENANT_ID`, `ENTRA_AUTHORITY`,
`ENTRA_BACKEND_CLIENT_ID`, `ENTRA_API_SCOPE`, `APPLICATIONINSIGHTS_FRONTEND_CONNECTION_STRING`).

```bash
docker build -f deployment/docker/frontend.Dockerfile \
  --build-arg ENTRA_SPA_CLIENT_ID=<spa-client-id> \
  --build-arg ENTRA_TENANT_ID=<tenant-id> \
  -t <acr-name>.azurecr.io/web:latest .

az acr login --name <acr-name>
docker push <acr-name>.azurecr.io/web:latest

az webapp restart -g rg-my-app -n my-foundry-agent-app
```

## Known limitations on App Service

- **No scale-to-zero** — App Service Basic/Standard tiers keep at least one instance running;
  cost behaves differently than Container Apps' consumption-based scale-to-zero.
- **No built-in FIC/RBAC automation** — the steps above are manual; nothing in this repo
  (`azd up`, Bicep, hooks) targets App Service today.
- **VNet integration** differs from the Container Apps `vnetInfrastructureSubnetId` parameter in
  [`infra/main.bicep`](../../infra/main.bicep) — App Service uses regional VNet Integration, a
  separate configuration surface (`az webapp vnet-integration add`).

For the fully automated path, use `azd up` with the Container Apps infrastructure in `infra/`.
