# GSC Reporter Workspace Instructions

## Project Purpose
- Automate recurring performance reporting to Slack.
- Current scope is broader than Search Console only: organic search, paid ads, and AI referral traffic are combined into one report.

## Architecture
- `GSCReporter.Core`: console runner for local/manual execution.
- `GSCReporter.AzureFunctions`: scheduled and HTTP-triggered execution.
- `GSCReporter.Services`: shared domain logic, API integrations, formatting, and orchestration.
- Keep business logic in `GSCReporter.Services`; entry projects should stay thin and focused on DI/hosting.

## Build And Run
- Build all: `dotnet build`
- Run console flow: `cd GSCReporter.Core && dotnet run`
- Build Functions project: `cd GSCReporter.AzureFunctions && dotnet build`
- Run Functions locally: `cd GSCReporter.AzureFunctions && func start`
- Publish Functions: `cd GSCReporter.AzureFunctions && dotnet publish --configuration Release`
- There is currently no test project. If behavior changes, run at least one local end-to-end execution through `GSCReporter.Core`.

## Service Boundaries
- `ReportService` orchestrates period calculation, data fetches, aggregation, and Slack dispatch.
- `SearchConsoleService` owns Google Search Console access.
- `GoogleAnalyticsService` owns GA paid + AI traffic retrieval.
- `SlackService` owns final message formatting and webhook delivery.

## Conventions
- Use dependency injection and interfaces (`I*`) for all service dependencies.
- Keep async APIs asynchronous end-to-end (`*Async`).
- Use structured `ILogger` calls with context values.
- Environment variables override local file config; preserve current precedence behavior in both entry points.
- Google Analytics is optional; failures there should not block sending the rest of the report.

## Source Of Truth Files
- Runtime schedule: `GSCReporter.AzureFunctions/Functions/ReportFunction.cs`
- DI and config wiring: `GSCReporter.Core/Program.cs`, `GSCReporter.AzureFunctions/Program.cs`
- Data contracts: `GSCReporter.Services/Models/*`
- Shared config and target markets: `GSCReporter.Services/Configuration/AppConfig.cs`, `GSCReporter.Services/Configuration/TargetMarkets.cs`
- Product/business context: `README.md`

## Maintenance Rules For Agent Edits
- Keep instructions concise; add only conventions that apply across most tasks.
- Prefer linking to source files instead of duplicating long documentation.
- When changing schedules, configuration keys, or reporting sections, update both code and docs together (`README.md`, `CLAUDE.md`, this file).
- Do not commit secrets, key JSON files, or local settings.
