using GSCReporter.Services.Configuration;
using GSCReporter.Services.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices(services =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();
        
        services.Configure<AppConfig>(config =>
        {
            var keyJson = Environment.GetEnvironmentVariable("GOOGLE_SERVICE_ACCOUNT_KEY_JSON") ?? "";
            var keyPath = Environment.GetEnvironmentVariable("GOOGLE_SERVICE_ACCOUNT_KEY_PATH") ?? "";

            config.SearchConsole.ServiceAccountKeyPath = keyPath;
            config.SearchConsole.ServiceAccountKeyJson = keyJson;
            config.SearchConsole.SiteUrl = Environment.GetEnvironmentVariable("GOOGLE_SITE_URL") ?? "";
            config.Slack.WebhookUrl = Environment.GetEnvironmentVariable("SLACK_WEBHOOK_URL") ?? "";
            config.Slack.Channel = Environment.GetEnvironmentVariable("SLACK_CHANNEL") ?? "#general";

            // Google Analytics configuration - reuses Search Console key by default
            config.GoogleAnalytics.PropertyId = Environment.GetEnvironmentVariable("GOOGLE_ANALYTICS_PROPERTY_ID") ?? "";
            config.GoogleAnalytics.ServiceAccountKeyJson = Environment.GetEnvironmentVariable("GOOGLE_ANALYTICS_KEY_JSON") ?? keyJson;
            config.GoogleAnalytics.ServiceAccountKeyPath = Environment.GetEnvironmentVariable("GOOGLE_ANALYTICS_KEY_PATH") ?? keyPath;
        });

        services.AddTransient<ISearchConsoleService, SearchConsoleService>();
        services.AddTransient<ISlackService, SlackService>();

        // Register Google Analytics service only if configured
        services.AddTransient<IGoogleAnalyticsService>(sp =>
        {
            var config = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<AppConfig>>();
            if (!string.IsNullOrEmpty(config.Value.GoogleAnalytics.PropertyId))
            {
                return new GoogleAnalyticsService(config, sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<GoogleAnalyticsService>>());
            }
            return null!;
        });

        services.AddTransient<IReportService, ReportService>();
    })
    .Build();

host.Run();