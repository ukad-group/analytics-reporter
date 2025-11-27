# GSC Reporter

Automated Google Search Console and Google Analytics reporting that sends beautiful Slack reports on a schedule.

## Why We Built This

We had a bi-weekly task of manually exporting Google Search Console statistics and posting them to Slack. It was tedious, repetitive, and easy to forget. So we automated it.

After using it internally for a while, we realized this could be useful to others facing the same problem. So here it is - feel free to use it, modify it, or build upon it.

## What It Does

- Fetches search performance data from Google Search Console API
- Tracks AI-driven traffic sources from Google Analytics (ChatGPT, Perplexity, Claude, etc.)
- Compares metrics with the previous period and shows percentage changes
- Sends formatted reports to Slack with clean tables
- Runs on a schedule via Azure Functions (or locally via console app)

Example Slack output:
```
GSC Report: 01.11 - 14.11.2025 vs 18.10 - 31.10.2025

| Country     | Clicks      | Impressions | CTR   | Position | AI Sessions |
|-------------|-------------|-------------|-------|----------|-------------|
| USA         | 245 (+12%)  | 15.2K (-3%) | 1.6%  | 8.2      | 34          |
| UK          | 89 (+5%)    | 4.1K (+8%)  | 2.2%  | 6.5      | 12          |
| Others      | 156 (-2%)   | 8.9K (+1%)  | 1.8%  | 11.3     | 8           |
```

## Architecture

```
GSC-Reporter/
├── GSCReporter.Core/           # Console app for local testing
├── GSCReporter.Services/       # Shared business logic
├── GSCReporter.AzureFunctions/ # Azure Functions for scheduled execution
├── azure-pipelines.yml         # CI/CD pipeline
└── azure-function-app.bicep    # Infrastructure as Code
```

## Quick Start

### Prerequisites

- .NET 8 SDK
- Google Cloud project with Search Console API enabled
- Service account with access to your Search Console property
- Slack workspace with Incoming Webhooks enabled
- (Optional) Azure subscription for deployment

### 1. Clone and Configure

```bash
git clone https://github.com/AliakseiDzi662662/gsc-reporter.git
cd gsc-reporter
```

Copy the example configuration:
```bash
cp GSCReporter.Core/appsettings.example.json GSCReporter.Core/appsettings.json
```

Edit `appsettings.json` with your credentials:
```json
{
  "SearchConsole": {
    "ServiceAccountKeyPath": "./your-service-account-key.json",
    "SiteUrl": "https://your-website.com"
  },
  "GoogleAnalytics": {
    "PropertyId": "YOUR_GA4_PROPERTY_ID",
    "ServiceAccountKeyPath": "./your-service-account-key.json"
  },
  "Slack": {
    "WebhookUrl": "https://hooks.slack.com/services/YOUR/SLACK/WEBHOOK",
    "Channel": "#your-channel"
  }
}
```

### 2. Set Up Google APIs

1. Go to [Google Cloud Console](https://console.cloud.google.com/)
2. Create a project and enable **Search Console API** and **Google Analytics Data API**
3. Create a service account and download the JSON key
4. Add the service account email to your [Search Console property](https://search.google.com/search-console) (Settings > Users)
5. Add the service account to your GA4 property (Admin > Property Access Management)

### 3. Set Up Slack

1. Create a [Slack App](https://api.slack.com/apps)
2. Enable **Incoming Webhooks**
3. Create a webhook for your channel and copy the URL

### 4. Run Locally

```bash
cd GSCReporter.Core
dotnet run
```

## Deployment to Azure

### Using Azure CLI

```bash
# Deploy infrastructure
az deployment group create \
  --resource-group your-rg \
  --template-file azure-function-app.bicep

# Set environment variables in Azure Portal or via CLI
az functionapp config appsettings set \
  --name your-function-app \
  --resource-group your-rg \
  --settings \
    "GOOGLE_SERVICE_ACCOUNT_KEY_JSON=<your-key-json>" \
    "GOOGLE_SITE_URL=https://your-website.com" \
    "SLACK_WEBHOOK_URL=https://hooks.slack.com/services/..." \
    "SLACK_CHANNEL=#your-channel"
```

### CI/CD

The included `azure-pipelines.yml` handles build and deployment. Configure your Azure DevOps pipeline to use it.

## Configuration Reference

### Environment Variables (Azure Functions)

| Variable | Description |
|----------|-------------|
| `GOOGLE_SERVICE_ACCOUNT_KEY_JSON` | Full JSON content of service account key |
| `GOOGLE_SERVICE_ACCOUNT_KEY_PATH` | Alternative: path to key file |
| `GOOGLE_SITE_URL` | Your website URL as it appears in Search Console |
| `SLACK_WEBHOOK_URL` | Slack incoming webhook URL |
| `SLACK_CHANNEL` | Target Slack channel |
| `GOOGLE_ANALYTICS_PROPERTY_ID` | GA4 property ID (optional) |

### Schedule

Default: 1st and 15th of each month at 9 AM (`0 0 9 1,15 * *`)

Modify in [ReportFunction.cs](GSCReporter.AzureFunctions/Functions/ReportFunction.cs):
```csharp
[TimerTrigger("0 0 9 1,15 * *")]
```

## Features

- **Period Comparison**: Automatically compares with the previous period of the same duration
- **Market Segmentation**: Groups data by target countries with "Others" aggregation
- **AI Traffic Tracking**: Tracks sessions from ChatGPT, Perplexity, Claude, Gemini, and Copilot
- **Graceful Degradation**: Google Analytics integration is optional - works with just Search Console
- **Clean Formatting**: Large numbers formatted with K/M suffixes, percentage changes with +/- indicators

## Customization

### Target Markets

Edit [TargetMarkets.cs](GSCReporter.Services/Configuration/TargetMarkets.cs) to change which countries are tracked individually:

```csharp
public static readonly List<string> Countries = new()
{
    "usa", "gbr", "irl", "bel", "nld", "swe", "dnk", "nor", "fin"
};
```

## Disclaimer

This software is provided "as is", without warranty of any kind. We built it for our own use and are sharing it in the hope that it might be useful to others. Use it at your own risk.

We take no responsibility for how you use this tool, any costs incurred from API usage, or any issues that may arise from its use.

## Contributing

Found a bug? Have an improvement? PRs are welcome.

## License

MIT License - see [LICENSE](LICENSE) for details.

---

Built with .NET 8, Azure Functions, and a healthy dislike for repetitive tasks.
