using GSCReporter.Services.Configuration;
using GSCReporter.Services.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        services.Configure<AppConfig>(context.Configuration);
        
        // Override with environment variables if available (for flexibility)
        services.PostConfigure<AppConfig>(config =>
        {
            var keyJson = Environment.GetEnvironmentVariable("GOOGLE_SERVICE_ACCOUNT_KEY_JSON");
            var keyPath = Environment.GetEnvironmentVariable("GOOGLE_SERVICE_ACCOUNT_KEY_PATH");
            var siteUrl = Environment.GetEnvironmentVariable("GOOGLE_SITE_URL");
            var webhookUrl = Environment.GetEnvironmentVariable("SLACK_WEBHOOK_URL");
            var channel = Environment.GetEnvironmentVariable("SLACK_CHANNEL");

            // Google Analytics configuration
            var gaPropertyId = Environment.GetEnvironmentVariable("GOOGLE_ANALYTICS_PROPERTY_ID");
            var gaKeyJson = Environment.GetEnvironmentVariable("GOOGLE_ANALYTICS_KEY_JSON");
            var gaKeyPath = Environment.GetEnvironmentVariable("GOOGLE_ANALYTICS_KEY_PATH");

            if (!string.IsNullOrEmpty(keyJson))
                config.SearchConsole.ServiceAccountKeyJson = keyJson;
            if (!string.IsNullOrEmpty(keyPath))
                config.SearchConsole.ServiceAccountKeyPath = keyPath;
            if (!string.IsNullOrEmpty(siteUrl))
                config.SearchConsole.SiteUrl = siteUrl;
            if (!string.IsNullOrEmpty(webhookUrl))
                config.Slack.WebhookUrl = webhookUrl;
            if (!string.IsNullOrEmpty(channel))
                config.Slack.Channel = channel;

            // Google Analytics - can reuse Search Console key if not specified separately
            if (!string.IsNullOrEmpty(gaPropertyId))
                config.GoogleAnalytics.PropertyId = gaPropertyId;
            if (!string.IsNullOrEmpty(gaKeyJson))
                config.GoogleAnalytics.ServiceAccountKeyJson = gaKeyJson;
            else if (!string.IsNullOrEmpty(keyJson))
                config.GoogleAnalytics.ServiceAccountKeyJson = keyJson;
            if (!string.IsNullOrEmpty(gaKeyPath))
                config.GoogleAnalytics.ServiceAccountKeyPath = gaKeyPath;
            else if (!string.IsNullOrEmpty(keyPath))
                config.GoogleAnalytics.ServiceAccountKeyPath = keyPath;
        });
        
        services.AddTransient<ISearchConsoleService, SearchConsoleService>();
        services.AddTransient<ISlackService, SlackService>();

        // Register Google Analytics service only if configured
        services.AddTransient<IGoogleAnalyticsService>(sp =>
        {
            var config = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<AppConfig>>();
            if (!string.IsNullOrEmpty(config.Value.GoogleAnalytics.PropertyId))
            {
                return new GoogleAnalyticsService(config, sp.GetRequiredService<ILogger<GoogleAnalyticsService>>());
            }
            return null!;
        });

        services.AddTransient<IReportService, ReportService>();
    })
    .Build();

var logger = host.Services.GetRequiredService<ILogger<Program>>();
var reportService = host.Services.GetRequiredService<IReportService>();

try
{
    logger.LogInformation("GSC Reporter starting...");
    await reportService.GenerateAndSendBiWeeklyReportAsync();
    logger.LogInformation("GSC Reporter completed successfully");
}
catch (Exception ex)
{
    logger.LogError(ex, "GSC Reporter failed");
    Environment.Exit(1);
}
