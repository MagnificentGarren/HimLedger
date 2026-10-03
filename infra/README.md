# Azure environments

`main.bicep` provisions the HimLedger API dependencies for one environment:
Linux App Service, Azure SQL Database, a private receipt container, Key Vault,
and Application Insights. Deploy `dev`, `staging`, and `prod` into **separate
resource groups**. The template is parameterized so the same reviewed
infrastructure definition is used in each environment.

The template creates a Basic SQL database and B1 App Service to keep a
portfolio deployment relatively low-cost. Check current Azure pricing and
quotas in your subscription before deploying. Upgrade the tiers, configure
availability-zone redundancy, and review network isolation before using this
as a real financial system.

## Prerequisites

- Azure CLI with Bicep support (`az bicep install` if needed).
- An Azure subscription with permission to create resources and role
  assignments.
- An Entra ID API registration with the authority and audience configured in
  the deployment.
- A frontend origin for the environment.

Create distinct resource groups and deploy each environment separately. The
first deployment must omit `appOutboundIps`; the template outputs the App
Service's possible outbound IPs but initially denies SQL and Storage access
from every public IP. Copy those addresses into `appOutboundIps` and deploy
the same template a second time to enable only the API's egress. Keep the
addresses in an ignored local parameter file or your secret manager, not in
source control. Set
`HIMLEDGER_SQL_ADMIN_PASSWORD` in the current PowerShell session using a
password manager; do not paste the value into a command, commit it, or place it
in a checked-in parameter file.

```powershell
az group create --name himledger-dev --location westeurope

az deployment group create `
  --name himledger-dev-infra `
  --resource-group himledger-dev `
  --template-file infra\main.bicep `
  --parameters namePrefix=himledger environmentName=dev `
    sqlAdminLogin=himledgeradmin `
    sqlAdminPassword="$env:HIMLEDGER_SQL_ADMIN_PASSWORD" `
    frontendOrigin=https://dev.example.com `
    entraAuthority=https://login.microsoftonline.com/YOUR_TENANT_ID/v2.0 `
    entraAudience=api://YOUR_API_CLIENT_ID

# Retrieve the output after the first deployment.
az deployment group show --resource-group himledger-dev `
  --name himledger-dev-infra `
  --query properties.outputs.appOutboundIpAddresses.value --output tsv

# Copy the output address list from the first deployment into this array.
az deployment group create `
  --name himledger-dev-infra `
  --resource-group himledger-dev `
  --template-file infra\main.bicep `
  --parameters namePrefix=himledger environmentName=dev `
    sqlAdminLogin=himledgeradmin `
    sqlAdminPassword="$env:HIMLEDGER_SQL_ADMIN_PASSWORD" `
    frontendOrigin=https://dev.example.com `
    entraAuthority=https://login.microsoftonline.com/YOUR_TENANT_ID/v2.0 `
    entraAudience=api://YOUR_API_CLIENT_ID `
    appOutboundIps='["203.0.113.10","203.0.113.11"]'
```

Replace the tenant, audience, and example outbound addresses with your own
values before running the commands. The example IPs are documentation-only.

Repeat with separate resource groups and `environmentName=staging` or
`environmentName=prod`, and use the matching frontend origin. Do not point
staging or production at development databases, storage, or Key Vaults. The
template restricts SQL and Storage ingress to the App Service's possible
outbound IP addresses; repeat the two-step deployment after a hosting-plan
change that changes those addresses.

After deployment:

1. Publish the API from a clean Release build, package the output, and deploy
   its ZIP package to the output `apiName`. For example:

   ```powershell
   dotnet publish backend\HimLedger.Api\HimLedger.Api.csproj `
     --configuration Release `
     --output "$env:TEMP\himledger-api-publish"
   Compress-Archive -Path "$env:TEMP\himledger-api-publish\*" `
     -DestinationPath "$env:TEMP\himledger-api.zip" -Force
   az webapp deploy --resource-group himledger-dev --name YOUR_API_NAME `
     --src-path "$env:TEMP\himledger-api.zip" --type zip
   ```

   The App Service is provisioned separately per environment.
2. Apply EF Core migrations deliberately from a trusted operator workstation
   or controlled release job. Review the generated SQL and migration locking
   implications before production; do not run migrations automatically at API
   startup.
3. Verify `/healthz` and `/ready` return `Healthy`. Confirm the App Service
   health check is enabled on `/healthz`, then check Application Insights for
   requests, dependency telemetry, and exceptions.
4. Confirm Key Vault references have resolved in App Service configuration.
   The app's managed identity receives the Key Vault Secrets User role. Role
   assignment propagation can take a few minutes.
5. Configure branch protection and GitHub deployment environments separately.
   Require review for `prod`; use GitHub OIDC federation rather than a stored
   Azure client secret if adding automated deployments.

## Operational limits to address before production

- The SQL administrator account is used by the API connection string in this
  portfolio template. Replace it with a least-privilege Entra workload
  identity and SQL database principal before production use.
- Azure Blob shared-key access is enabled because the current receipt service
  creates short-lived SAS URLs from the storage account key. Keep that key in
  Key Vault, rotate it deliberately, and consider user-delegation SAS with
  managed identity as a separate improvement.
- SQL is configured for point-in-time restore with the selected service tier's
  seven-day short-term retention and 24-hour differential backups. Choose a
  longer retention period and geo-redundancy if required by the actual
  recovery objectives; the inexpensive Basic tier is not a high-availability
  design.
- This template does not create a frontend host, configure custom domains,
  private endpoints, WAF, or a deployment slot. The frontend must be deployed
  separately and its exact HTTPS origin passed as `frontendOrigin`.
- Run the staging restore and rollback rehearsal in
  [`../backend/OPERATIONS.md`](../backend/OPERATIONS.md) before representing an
  environment as production-ready.
