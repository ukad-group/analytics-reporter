namespace GSCReporter.Services.Configuration;

public class AppConfig
{
    public SearchConsoleConfig SearchConsole { get; set; } = new();
    public GoogleAnalyticsConfig GoogleAnalytics { get; set; } = new();
    public SlackConfig Slack { get; set; } = new();
}

public class SearchConsoleConfig
{
    public string ServiceAccountKeyPath { get; set; } = string.Empty;
    public string ServiceAccountKeyJson { get; set; } = string.Empty;
    public string SiteUrl { get; set; } = string.Empty;
}

public class SlackConfig
{
    public string WebhookUrl { get; set; } = string.Empty;
    public string Channel { get; set; } = "#general";
}

public class GoogleAnalyticsConfig
{
    public string PropertyId { get; set; } = string.Empty;  // GA4 property ID (e.g., "270229075")
    public string ServiceAccountKeyPath { get; set; } = string.Empty;
    public string ServiceAccountKeyJson { get; set; } = string.Empty;
}