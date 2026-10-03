# HimLedger backend

## Lifecycle and financial controls

New claims are created as `Draft`. Submission records `Submitted` and then
`Pending Approval` in one transaction. Reviewers can approve, reject, or request
changes; employees can resubmit a claim after changes; Finance/Admin can mark an
approved claim reimbursed. Every state transition is written to the append-only
`ClaimStatusHistory` ledger. Database migrations install a SQL Server trigger
that rejects update and delete operations on that ledger.

Budget commitment, approval, reimbursement, and available values are derived
from claim transactions for the expense date's fiscal quarter. Claim approvals
run in a serializable transaction and are rejected if they would exceed the
department's allocation. Finance/Admin can export period-filtered reconciliation
CSV from `GET /api/Expenses/reconciliation.csv`; each row carries its claim
identity and transition history.

Potential duplicate matches use exact amount, normalized claim title as the
vendor field, and a seven-day expense-date window. They are reviewer alerts,
not automatic rejection.

## Receipt storage and notifications

Configure these environment variables (do not commit storage credentials):

- `ConnectionStrings__AzureBlobStorage`: Azure Blob Storage connection string
  using an account key, required for short-lived SAS generation.
- `AzureBlobStorage__ContainerName`: private container name. The API creates it
  with public access disabled if it does not already exist.
- `Notifications__WebhookUrl`: optional HTTP(S) notification receiver. Outbox
  events are committed with claim transitions and delivered asynchronously.
  Delivery retries use the same `Idempotency-Key`; the receiving service should
  deduplicate on that key. Failed deliveries are logged and retained for retry.

Receipt uploads accept PDF, JPEG, and PNG files up to 10 MiB. The API checks
file signatures, stores content in Azure Blob Storage, stores only blob metadata
in SQL Server, and issues read-only SAS URLs that expire after five minutes.

## Phase 6 operations

`GET /healthz` is the dependency-independent liveness probe. `GET /ready`
checks database connectivity and returns `503` if SQL Server is unavailable;
`GET /health` remains a readiness alias for compatibility. Logs use structured
JSON output, and Application Insights request/dependency/exception telemetry is
enabled when `ApplicationInsights:ConnectionString` is configured.

The parameterized Azure resources and deployment guidance are in
[`../infra/README.md`](../infra/README.md). The recovery, rollback, telemetry,
and staging-rehearsal procedures are in
[`OPERATIONS.md`](OPERATIONS.md). Deploy `dev`, `staging`, and `prod` into
separate resource groups. Do not treat infrastructure deployment as proof of
production readiness: complete and record the staging recovery/rollback
rehearsal first.

## Database

Apply the EF Core migrations from the backend directory with:

```powershell
dotnet ef database update --project HimLedger.Infrastructure --startup-project HimLedger.Api
```

For a fresh SQL Server database created from scratch, `HimLedger.Schema.sql`
includes the same account activation, in-app notification, and account-access
audit tables. Use EF migrations to upgrade an existing database.

The latest migration adds per-account notifications, account activation state,
and an audit trail for account access changes. Admins should deactivate accounts
from the User roles workspace instead of deleting them; claim and audit history
is retained, and the last active Admin account cannot be deactivated.

## Local role-testing accounts

The API's Development launch profiles create any missing role-testing accounts
after the database migrations have been applied. Password login and this seeding
are available only in the Development environment; do not reuse these credentials
outside local testing.

| Role | Email (username) | Password |
| --- | --- | --- |
| Admin | `admin.test@himledger.local` | `Test123!` |
| Manager | `manager.test@himledger.local` | `Test123!` |
| Employee | `employee.test@himledger.local` | `Test123!` |
| Finance | `finance.test@himledger.local` | `Test123!` |

Manager and Employee accounts are assigned to the first configured department.
If an account already exists, startup leaves it unchanged. These accounts are
not seeded when the API runs outside Development.

`HimLedger.Api.http` contains sample requests for the lifecycle and reconciliation
routes.
