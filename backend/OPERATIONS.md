# Operations runbook

This runbook is a starting point for the Azure resources in
[`../infra/main.bicep`](../infra/main.bicep). Fill in environment-specific
resource group and resource names in your team's secret manager; never commit
credentials or production identifiers that should remain private.

## Health and telemetry

- `GET /healthz` is a liveness probe. It confirms the API process is running
  and intentionally does not check dependencies.
- `GET /ready` checks database connectivity and returns `503` when the
  database health check is unhealthy. `/health` remains as a compatibility
  alias for readiness.
- Logs are emitted as structured JSON to standard output. When
  `ApplicationInsights:ConnectionString` is configured, the Application
  Insights SDK captures request, dependency, and exception telemetry.
- On an alert, check App Service availability and recent deployments first,
  then inspect Application Insights failures/dependencies and the SQL
  resource's health. Do not put claim, receipt, token, or connection-string
  contents in telemetry.

## Release and rollback

1. Promote the same tested Release artifact through isolated `dev`, `staging`,
   and `prod` resource groups. Keep each environment's database, storage,
   secrets, and frontend origin separate.
2. Before a production release, deploy to staging, verify `/healthz` and
   `/ready`, exercise sign-in and a representative claim workflow, and inspect
   telemetry. Record the artifact version and migration being released.
3. Apply schema changes using an expand/contract migration: add backward
   compatible schema first, deploy code that can use it, then remove obsolete
   schema only in a later release. Take and verify a recovery point before a
   destructive migration. The API does not apply migrations automatically.
4. If the release fails, redeploy the last known-good API artifact with
   `az webapp deploy --resource-group YOUR_RESOURCE_GROUP --name YOUR_API_NAME --src-path YOUR_KNOWN_GOOD_ZIP --type zip`.
   Confirm `/healthz`, `/ready`, and the representative workflow. Roll back
   database changes only when the migration has a tested reverse path; a
   database restore replaces the target database and can discard writes made
   after the recovery point.

## Database recovery rehearsal

Azure SQL performs automated backups according to the selected service tier's
policy. Verify the configured retention and geo-redundancy against the
environment's recovery objectives before relying on them.

At least once before launch, restore to a **new** database in a non-production
environment and verify that the application can connect and read expected
reference data. For example, restore a point-in-time copy from a protected
operator shell:

```powershell
az sql db restore `
  --resource-group YOUR_RESOURCE_GROUP `
  --server YOUR_SQL_SERVER_NAME `
  --name HimLedger `
  --dest-name HimLedger-restore-check `
  --time 2026-10-02T20:00:00Z
```

Wait for the restored database to become available, temporarily configure a
non-production API to use it, then verify `/ready` and read-only application
flows. Record the recovery point, elapsed restore time, and validation result.
Delete only the specifically named restore-check database after the exercise
is signed off.

For an incident affecting production, stop writes or put the application into
maintenance mode before restoring. Restore to a new database first, validate
it, then coordinate the connection-string switch and user communications.
Never overwrite or delete the original database until the incident owner has
approved the recovery.

## Staging rehearsal checklist

- [ ] Deploy infrastructure and the exact candidate artifact to staging.
- [ ] Verify the stage has its own database, private receipt container,
      Key Vault, and Application Insights resource.
- [ ] Confirm secret references resolve and no secret appears in logs.
- [ ] Verify liveness stays healthy while a deliberately unavailable database
      makes readiness fail (covered by the API integration test).
- [ ] Restore a database copy to a new staging database and validate it.
- [ ] Redeploy the prior artifact and verify health and a representative flow.
- [ ] Record who ran the rehearsal, the duration, issues, and follow-up actions.
