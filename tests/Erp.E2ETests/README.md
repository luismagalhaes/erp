# Erp.E2ETests

Browser end-to-end tests for `Erp.Main` (and, through its login redirect, `Erp.Identity`) using
[Playwright](https://playwright.dev/dotnet/).

## Layout

```
Configuration/   E2ESettings, TestUserSettings — binds appsettings.json + env vars
Fixtures/        PlaywrightFixture (browser lifecycle), PlaywrightCollection (xunit collection)
Support/         Shared setup helpers (companies, series, MudSelect interaction) — see below
Identity/        Login flow
Core/            Companies, customers
SeriesRegistry/  Series
Sales/           Stock movements, invoices, ...
```

A new feature area gets its own top-level folder, matching the app's own module boundaries — not
one flat folder of test classes.

### Support/ — shared setup

Almost every business-flow page requires a company to already exist and be selected, and nothing
is seeded by default. `Support/CompanyTestHelper.CreateCompanyWithAccessAsync` creates one and
grants the current user access (creating a company does **not** do this automatically — see the
comment there), and `Support/SeriesTestHelper.CreateSeriesAsync` creates and "communicates" a
series (a new series can't issue anything until that step, even in a dev/staging environment with
no real AT webservice call behind it — see the comment there). Every business test other than the
one exercising creation itself should start from these instead of repeating the flow.

`Support/MudSelectExtensions.cs` and `Support/TestData.cs` explain two MudBlazor/Playwright quirks
worth knowing before writing a new test:

- MudBlazor's MudSelect binds through a `type="hidden"` input, so `Page.GetByLabel` never finds
  it — use `page.MudSelectByLabel(...)` / `page.SelectMudOptionAsync(...)` instead.
- A `MudTextField` without `Immediate="true"` binds on blur, and `FillAsync` alone never triggers
  that — call `.BlurAsync()` on the field after filling it, before moving on.
- Test data must not collide across repeated runs against a shared, never-cleaned-up environment —
  `TestData.UniqueSuffix()` / `UniqueDigits()` exist because a truncated timestamp collided within
  minutes during development (a series code collision fails the whole request with a 500).

### Known bug found by these tests

Issuing a stock movement (and, by the same code path, any signed sales document — invoices,
credit notes, receipts) currently fails against a real signing key: `Hash`/`PreviousHash` are
`HasMaxLength(200)` in
[SalesModelConfiguration.cs](../../src/Modules/Erp.Sales/Storage/Data/SalesModelConfiguration.cs),
but a real RSA-2048 signature's Base64 encoding is 344 characters, so the save 500s. This is being
addressed separately on the frontend/schema side, so
`Signed_in_user_can_issue_a_stock_movement` only asserts the form fills in correctly and is ready
to submit — not that issuing actually succeeds — until then. See the comment in
`Sales/StockMovementTests.cs`.

## Setup

1. Build the project once so the Playwright CLI script is generated:
   ```
   dotnet build tests/Erp.E2ETests
   ```
2. Install the Playwright browsers (one-time, or after a Playwright version bump):
   ```
   pwsh tests/Erp.E2ETests/bin/Debug/net10.0/playwright.ps1 install
   ```
3. Run `Erp.Identity` and `Erp.Main` locally (defaults expected at `https://localhost:7081` and
   `https://localhost:7019` — see `appsettings.json`).

## Running the tests

```
dotnet test tests/Erp.E2ETests
```

Tests are tagged `Category=E2E` and excluded from the solution-wide `dotnet test Erp.slnx --filter "Category!=E2E"` used elsewhere, since they need the hosts running locally.

**From VS Code**: `Ctrl+Shift+P` → `Tasks: Run Task` → **E2E: Run tests (headed)** opens a visible
Chromium window and runs every test in it — no need to edit `appsettings.local.json` first, it
overrides `Headless` for that run only via `-e E2E__Headless=false`. **E2E: Run tests (headless)**
runs the same tests the way CI does. Both tasks restore from nuget.org explicitly first — on a
machine where the default NuGet source is a corporate feed that 401s, plain `dotnet test` fails to
restore. See [.vscode/tasks.json](../../.vscode/tasks.json).

The sign-in redirect test needs no credentials. The full sign-in flow test needs a test user;
configure it in an untracked `appsettings.local.json` next to `appsettings.json`:

```json
{
  "E2E": {
    "TestUser": {
      "Email": "user@example.com",
      "Password": "..."
    }
  }
}
```

Without it, that test is a no-op (it logs a skip message and passes).

## Running in CI

The `e2e-tests` job in [dotnet-ci.yml](../../.github/workflows/dotnet-ci.yml) runs these tests
against the real staging deployment right after `deploy` succeeds — staging only, and only on
`workflow_dispatch`. Configuration there comes entirely from environment variables (no
`appsettings.local.json` on a CI runner):

- `E2E__MainBaseUrl` / `E2E__IdentityBaseUrl` — the staging Web App URLs, hardcoded in the workflow
  (not secrets: they're public URLs).
- `E2E__TestUser__Email` / `E2E__TestUser__Password` — from the `E2E_STAGING_TEST_USER_EMAIL` /
  `E2E_STAGING_TEST_USER_PASSWORD` repository secrets. Without them the login test skips itself;
  only the redirect smoke test runs.

The double-underscore (`E2E__Key`) is the standard .NET convention for overriding a nested
configuration section through an environment variable — a single underscore would land the value
at the configuration root instead of under the `E2E` section this project binds to.

## Watching a run — Trace Viewer

.NET's Playwright has no equivalent to the TypeScript test runner's UI Mode, so this is the
closest thing to Cypress's step-by-step run history: every test records a trace (screenshots
before/after each action, DOM snapshots, network, console) to
`bin/<Configuration>/net10.0/playwright-traces/<TestName>.zip` — pass or fail, not just on
failure.

Open one at [trace.playwright.dev](https://trace.playwright.dev) (drag the `.zip` in — it runs
entirely in your browser, nothing is uploaded) or locally with:

```
npx playwright show-trace tests/Erp.E2ETests/bin/Debug/net10.0/playwright-traces/<TestName>.zip
```

In CI, the `e2e-tests` job renames each known test's trace to `e2e-report-<name>.zip` (the
C# test method name would otherwise leak into the artifact's display name — `archive: false`
names the artifact after the file itself) and uploads it as its **own** artifact — download
`e2e-report-login-redirect.zip` or `e2e-report-sign-in.zip` from the run's **Summary** page and
drag it straight into trace.playwright.dev, no unzipping needed. There's also a `playwright-traces`
artifact bundling every trace file found,
kept as a safety net for a test added without a matching upload step in the workflow — that one
needs unzipping twice (GitHub wraps the artifact, and each trace is already a `.zip`).

For live, step-by-step debugging while a test runs locally (closer to Cypress's interactive
mode), add `await page.PauseAsync();` at the point you want to inspect and run with headed mode
on (see `appsettings.local.json` above) — it opens the Playwright Inspector.
