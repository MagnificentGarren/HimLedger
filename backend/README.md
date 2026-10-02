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

## Database

Apply the EF Core migrations from the backend directory with:

```powershell
dotnet ef database update --project HimLedger.Infrastructure --startup-project HimLedger.Api
```

`HimLedger.Api.http` contains sample requests for the lifecycle and reconciliation
routes.
