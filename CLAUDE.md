# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

GSC Reporter is an automated Google Search Console reporting solution that generates bi-monthly reports and sends them to Slack. It fetches search performance data, compares it with the previous period, and formats it as readable tables with percentage changes.

## Architecture

This solution follows a clean 3-project architecture:

- **GSCReporter.Core**: Console application for local testing and manual report generation
- **GSCReporter.Services**: Shared business logic containing all services, models, and configuration
- **GSCReporter.AzureFunctions**: Azure Functions with timer and HTTP triggers for automated scheduling

### Key Services
- **ReportService**: Orchestrates the entire report generation flow
- **SearchConsoleService**: Handles Google Search Console API integration and data fetching
- **GoogleAnalyticsService**: Handles Google Analytics Data API integration for AI traffic sources
- **SlackService**: Manages Slack webhook communication and message formatting

### Data Flow
Timer/HTTP Trigger → ReportService → SearchConsoleService + GoogleAnalyticsService (fetches current + previous period) → AggregatedReport → SlackService (formats merged table and sends to Slack)

### Key Models
- **AggregatedReport**: Container combining data from all sources (GSC + GA)
- **SearchConsoleReport**: GSC clicks, impressions, CTR by market
- **AITrafficReport**: AI traffic sessions by country and source

## Development Commands

### Local Development
```bash
# Build entire solution
dotnet build

# Run console app for testing
cd GSCReporter.Core && dotnet run

# Run Azure Functions locally
cd GSCReporter.AzureFunctions && func start
```

### Deployment
```bash
# Deploy infrastructure
az deployment group create --resource-group your-rg --template-file azure-function-app.bicep

# CI/CD via Azure Pipelines (see azure-pipelines.yml)
```

## Configuration

### Local Development (appsettings.json)
```json
{
  "SearchConsole": {
    "ServiceAccountKeyPath": "path/to/service-account-key.json",
    "SiteUrl": "https://your-website.com"
  },
  "GoogleAnalytics": {
    "PropertyId": "YOUR_GA4_PROPERTY_ID",
    "ServiceAccountKeyPath": "path/to/service-account-key.json"
  },
  "Slack": {
    "WebhookUrl": "https://hooks.slack.com/services/...",
    "Channel": "#general"
  }
}
```

### Azure Functions (Environment Variables)
- `GOOGLE_SERVICE_ACCOUNT_KEY_JSON` or `GOOGLE_SERVICE_ACCOUNT_KEY_PATH`
- `GOOGLE_SITE_URL`
- `SLACK_WEBHOOK_URL`
- `SLACK_CHANNEL`
- `GOOGLE_ANALYTICS_PROPERTY_ID` (GA4 property ID)
- `GOOGLE_ANALYTICS_KEY_JSON` or `GOOGLE_ANALYTICS_KEY_PATH` (optional - reuses Search Console key if not specified)

The configuration system supports both file-based (local) and environment variable-based (Azure) approaches, with environment variables taking precedence.

## Key Implementation Details

### Google Search Console Integration
- Fetches data for both current and previous periods for comparison
- Supports both service account key files and JSON strings for authentication
- Handles market-specific statistics by country with aggregation

### Report Scheduling
- **Timer Trigger**: `"0 0 9 1,15 * *"` (1st and 15th of each month at 9 AM)
- **Manual Trigger**: HTTP endpoint for on-demand report generation
- **Period Calculation**: Automatically calculates previous period of same duration

### Google Analytics Integration (AI Traffic)
- Fetches traffic acquisition data to track AI-driven engagement by country
- Categorizes referral sources: ChatGPT, Perplexity, Claude, Gemini, Copilot, Other AI
- Returns data grouped by country and AI source for merged reporting
- Filters to target markets only (matching GSC countries)
- Optional feature - gracefully skips if not configured

### Slack Message Formatting
- Single merged table combining GSC metrics with AI traffic columns
- Dynamic AI columns - only shows platforms with actual data
- Shows percentage changes with +/- indicators
- Groups countries by priority (target markets first, then Others)
- Formats large numbers with K/M suffixes

### Shared Configuration
- `TargetMarkets.cs` defines target countries used by both GSC and GA services
- Country codes (GSC) and country names (GA) mapped to consistent display names

### Error Handling
- Comprehensive logging with structured ILogger throughout
- Proper exception propagation with context
- Graceful handling of missing data scenarios

## Testing

The console application (`GSCReporter.Core`) serves as the primary testing mechanism. It uses the same services as the Azure Functions, making it reliable for validating changes before deployment.

## Infrastructure

- **Azure Function App** with consumption plan
- **Application Insights** for monitoring and telemetry  
- **Azure Storage** for Functions runtime
- **Bicep template** for Infrastructure as Code
- **Azure Pipelines** for CI/CD automation

## Commit Guidelines

Do not include Claude Code attribution or generation messages in commit messages. Keep commits clean and focused on the actual changes made.