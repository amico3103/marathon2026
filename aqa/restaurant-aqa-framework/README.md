# Restaurant AQA framework

End-to-end tests for the Green & Tasty restaurant application: Reqnroll (Gherkin) + TUnit + Playwright for .NET, with the Screenplay pattern.

Guidance for extending the suite (writing Gherkin, steps, Screenplay, test data, working with an AI assistant) is in [CLAUDE.md](CLAUDE.md).

## Group pre-order homework progress

Direction 1, levels 1–3 are planned in the [coverage matrix](docs/coverage-matrix.md).
The [environment setup](docs/environment-setup.md), [waiter account checks](docs/waiter-account-check.md),
and [first browser smoke run](docs/first-smoke-run.md) are documented separately.

The [owned test-data lifecycle](docs/owned-test-data.md) prepares isolated customers,
tables, reservations and carts, and removes their records using scoped AWS SDK calls.
Its two live checks pass. [Level 1 is implemented](docs/level-one-implementation.md):
14 examples cover the 11 matrix items. The [complete L1 run](docs/level-one-results.md)
passed all 14 examples with no failures or skips (step 8).
[Level 2](docs/level-two-results.md) adds 15 examples for submitting a consolidated
order and reviewing it as the assigned waiter. All 15 have passed, including
a targeted rerun after correcting one expected error message in the test.
[Level 3](docs/level-three-results.md) adds 15 examples for guest removal,
totals and access restrictions. All 15 have passed, including a targeted rerun
after correcting the current-group selection in the two-group test setup.
The [combined E2E journey](docs/end-to-end-journey.md) has passed: two guests
order through the UI, the owner removes one and submits, and the assigned waiter
sees only the retained dishes. Four browser traces are archived locally.
The [combined execution checks](docs/repeatability-and-parallelism.md) passed
all 45 examples sequentially. Two parallel attempts confirmed concurrency 2,
but finished with transport failures (44/45 and 43/45 passed); a stable complete
parallel run is still unconfirmed. The local default remains 1.
The [GitHub Actions run](https://github.com/amico3103/marathon2026/actions/runs/41659883656)
passed all 45 examples and published a token-safe summary. The final reviewer
guide remains for step 15.
These fixtures require AWS credentials in addition to the UI/API and waiter settings;
the required parameters are listed in `.env.example`. The basic smoke test needs no AWS credentials.

## Run the tests

```bash
cp .env.example .env        # then fill in your own values (see "Configuration")
dotnet build
dotnet run --no-build -- --treenode-filter "/*/*/*/*[Category=group-preorder]" --report-trx # all 45 homework examples
dotnet run --no-build -- --treenode-filter "/*/*/*/*[Category=smoke]"   # by tag
```

Everything a run produces lands in `TestResults/`: the TUnit report and TRX, the Reqnroll report in `reqnroll/`, and Playwright traces in `traces/` (open one with `npx playwright show-trace <file>.zip`).

The original framework features include `@known-defect` and `@wip` scenarios,
and admin scenarios need a configured admin account. The `group-preorder`
command selects the 45 implemented homework examples; it needs the environment,
waiter and AWS settings described above.

## Quality gates

| Where | What runs |
|---|---|
| Git hooks (local) | `pre-commit`: `dotnet format` check and Gherkin lint on staged files · `commit-msg`: Conventional Commits · `pre-push`: build with analyzers |
| `aqa-quality.yml` | on every push and pull request: no generated files tracked, format, build (warnings are errors), Gherkin lint, test discovery |
| `aqa-tests.yml` | all 45 homework scenarios, sequentially, nightly and on demand (Actions → AQA Tests → Run workflow); uploads a safe HTML/JSON summary even on failure |

Enable the hooks once per clone, from the repository root:

```bash
dotnet tool restore --tool-manifest aqa/restaurant-aqa-framework/.config/dotnet-tools.json
git config core.hooksPath aqa/restaurant-aqa-framework/.husky
```

The hooks only act on commits and pushes that touch `aqa/restaurant-aqa-framework`. Gherkin lint needs Node.js.

## Configuration

There is no shared environment and no committed `.env`: copy `.env.example` to `.env` and fill it in with the data of **your own** deployment (see "Testing your own deployment"). `.env` is git-ignored; never commit it.

Settings come from `.env` or real environment variables (variables win):

| Setting | Meaning |
|---|---|
| `UI_URL` | where the frontend is served |
| `API_URL` | base URL of the backend API, including the stage (for example `…/develop`) |
| `WAITER_USERNAME`, `WAITER_PASSWORD` | a waiter account that already exists in the environment under test |
| `TESTTABLE_ID`, `TESTTABLE_WAITER_*` | a table and the waiter assigned to it (only for scenarios that need them) |
| `ADMIN_USERNAME`, `ADMIN_PASSWORD` | an admin account; without it the admin scenarios are skipped |

The full list, with defaults, is in `.env.example`.

## Testing your own deployment

### If you deployed the backend / frontend / DevOps homework

1. `cp .env.example .env` and set `UI_URL` and `API_URL` to your deployment (API URL includes the stage, for example `…/develop`).
2. Waiters cannot be created by signing up, so create a waiter (and, for the admin scenarios, an admin) in *your* environment and put its credentials in `WAITER_*` / `ADMIN_*`. Optionally add a table and its assigned waiter (`TESTTABLE_*`).
3. Run `dotnet build` and a small slice first (`--treenode-filter "/*/*/*/*[Category=smoke]"`).

### If you have not deployed your own homework

If you deployed the original application, test that: set `UI_URL` / `API_URL` as above. Otherwise point the framework at a comparable public site (Booking, Airbnb, OpenTable, …) and write all the layers for it yourself: Gherkin features, step definitions, Screenplay tasks and questions, page objects with locators, and API clients where the site has a usable API. Use only flows you are allowed to automate (no purchases, no real bookings, respect the site's terms), and set `UI_URL` to that site.

1. **Generate a skeleton with Playwright codegen** (see "Presetup instructions"): record one flow, for example search → open a result → check details. Save the generated code to a scratch file, it is only raw material.
2. **Split it across the layers**: locators and small actions go to `Ui/` page objects, the flow becomes a task in `Screenplay/Tasks/`, the read-backs become questions in `Screenplay/Questions/`, the business wording goes to `Features/` and thin glue to `StepDefinitions/`.
3. **Replace weak locators.** Codegen often emits positional or text-fragile selectors. Prefer role + accessible name, label, test id, then CSS (see [CLAUDE.md](CLAUDE.md)); check them in the Playwright inspector.
4. Tag scenarios `@ui` / `@api`, keep them independent, and run `npx gherkin-lint -c .gherkin-lintrc "Features/*.feature"`.

### CI on your own public repository

Publish your work in your own public Git repository (the link is the deliverable). Workflows are disabled in a fresh fork until you enable them. The repository's `.github/workflows/aqa-quality.yml` checks source on pushes and pull requests; `.github/workflows/aqa-tests.yml` runs the 45 live scenarios nightly or with **Actions → AQA Tests → Run workflow**. Configure these values under **Settings → Secrets and variables → Actions** before the live run:

- **Variables:** `AQA_UI_URL`, `AQA_API_URL`, `AQA_AWS_REGION`, `AQA_AWS_ACCOUNT_ID`, `AQA_COGNITO_USER_POOL_ID`, `AQA_TESTTABLE_ID`.
- **Secrets:** `AQA_WAITER_USERNAME`, `AQA_WAITER_PASSWORD`, `AQA_AWS_ACCESS_KEY_ID`, `AQA_AWS_SECRET_ACCESS_KEY`.

The test workflow reuses the standing waiter's credentials for `TESTTABLE_WAITER_*`, uses AWS credentials only in the test step, and fixes `MAX_PARALLEL_TESTS=1`. Those AWS credentials must permit the scoped Cognito and DynamoDB fixture lifecycle described in [owned test data](docs/owned-test-data.md). The current AWS account explicitly denies OIDC provider inspection to our local IAM user; GitHub role setup has not been completed. Never put credentials in the repository, in `.env.example`, or in test code.

Download **aqa-group-preorder-report** from the [successful run](https://github.com/amico3103/marathon2026/actions/runs/41659883656) to open `index.html` and inspect `summary.json`: all 45 examples passed. The workflow intentionally keeps raw TRX, runner logs, Playwright traces and owned-data journals out of the downloadable artifact because those may contain authentication tokens. A failed test still makes the workflow fail after the summary has been uploaded. Manual runs can select one tagged example and its expected count for diagnosis; the default is the complete 45-example set. [CI verification notes](docs/ci-and-reporting.md) include the earlier failure and its fix.

## Presetup instructions

This framework contains PlaywriteInstaller script so you don't need to setup it manually.
For full BDD highlighting in your IDE you may need to install Reqnroll extension https://docs.reqnroll.net/latest/ide-integrations/index.html
To start the Playwright code generator open Powershell in the framework directory and enter the line:
```bash
bin/Debug/net10.0/playwright.ps1 codegen "<your UI_URL or the public site you test>"
```
Build first so `playwright.ps1` exists. The URL is optional.
