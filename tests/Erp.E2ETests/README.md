# Erp.E2ETests

Browser end-to-end tests for `Erp.Main` (and, through its login redirect, `Erp.Identity`) using
[Playwright](https://playwright.dev/dotnet/).

## Layout

```
Configuration/   E2ESettings, TestUserSettings — binds appsettings.json + env vars
Fixtures/        PlaywrightFixture (browser lifecycle), PlaywrightCollection (xunit collection)
Identity/        Test specs for the Erp.Identity login flow (one folder per feature under test)
```

A new feature area gets its own top-level folder next to `Identity/`, matching the app's own
module boundaries — not one flat folder of test classes.

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
