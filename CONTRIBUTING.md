# Development checks

Run the same checks used by CI before opening a pull request.

## Backend

Requires the .NET 8 SDK.

```powershell
dotnet restore backend\HimLedger.sln
dotnet build backend\HimLedger.sln --configuration Release
dotnet test backend\HimLedger.sln --configuration Release
```

Package versions for backend projects are managed in
[`backend/Directory.Packages.props`](./backend/Directory.Packages.props). Add
new package versions there rather than pinning versions in project files.

## Frontend

Requires Node.js 22.

```powershell
Set-Location frontend
npm ci
npm run lint
npm run test:unit
npm run build
npm audit --audit-level=high
```

The browser end-to-end tests use Playwright. Install its Chromium browser once
with `npx playwright install chromium`, then run `npm run test:e2e`.

## CI coverage

Pull requests run .NET restore/build/tests, frontend lint/unit/build/audit,
Playwright end-to-end tests, CodeQL analysis, dependency review, and Gitleaks
secret scanning. Configure repository branch protection to require these
checks before merging.

List endpoints use `page` and `pageSize` query parameters and return
`items`, `page`, `pageSize`, and `totalCount`. The frontend API helper walks
all pages to preserve the current full-list views.
