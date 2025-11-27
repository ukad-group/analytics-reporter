using GSCReporter.Services.Models;
using Microsoft.Extensions.Logging;

namespace GSCReporter.Services.Services;

public class ReportService : IReportService
{
    private readonly ISearchConsoleService _searchConsoleService;
    private readonly IGoogleAnalyticsService? _googleAnalyticsService;
    private readonly ISlackService _slackService;
    private readonly ILogger<ReportService> _logger;

    public ReportService(
        ISearchConsoleService searchConsoleService,
        ISlackService slackService,
        ILogger<ReportService> logger,
        IGoogleAnalyticsService? googleAnalyticsService = null)
    {
        _searchConsoleService = searchConsoleService;
        _slackService = slackService;
        _logger = logger;
        _googleAnalyticsService = googleAnalyticsService;
    }

    public async Task GenerateAndSendBiWeeklyReportAsync()
    {
        try
        {
            _logger.LogInformation("Starting bi-weekly report generation");

            var endDate = DateTime.Today.AddDays(-3);  // 3-day lag to account for API data processing delays
            var startDate = endDate.AddDays(-13);

            _logger.LogInformation("Report period: {StartDate} - {EndDate}",
                startDate.ToString("yyyy-MM-dd"), endDate.ToString("yyyy-MM-dd"));

            // Build aggregated report from multiple data sources
            var aggregatedReport = new AggregatedReport
            {
                StartDate = startDate,
                EndDate = endDate
            };

            // Fetch Search Console data
            try
            {
                _logger.LogInformation("Fetching Search Console data");
                aggregatedReport.SearchConsole = await _searchConsoleService.GetReportAsync(startDate, endDate);
                _logger.LogInformation("Search Console data retrieved: {Clicks} clicks, {Impressions} impressions",
                    aggregatedReport.SearchConsole.TotalClicks, aggregatedReport.SearchConsole.TotalImpressions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch Search Console data");
                throw; // GSC data is required
            }

            // Fetch AI traffic data from Google Analytics if configured
            if (_googleAnalyticsService != null)
            {
                try
                {
                    _logger.LogInformation("Fetching AI traffic data from Google Analytics");
                    aggregatedReport.AITraffic = await _googleAnalyticsService.GetAITrafficReportAsync(startDate, endDate);
                    var totalSessions = aggregatedReport.AITraffic.SessionsByCountryAndSource.Values.SelectMany(v => v.Values).Sum();
                    var countryCount = aggregatedReport.AITraffic.SessionsByCountryAndSource.Count;
                    _logger.LogInformation("AI traffic data retrieved: {TotalSessions} total sessions from {CountryCount} countries",
                        totalSessions, countryCount);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to fetch AI traffic data from Google Analytics. Continuing without it.");
                    // Don't fail the entire report if GA data is unavailable
                }
            }
            else
            {
                _logger.LogInformation("Google Analytics service not configured, skipping AI traffic data");
            }

            await _slackService.SendReportAsync(aggregatedReport);

            _logger.LogInformation("Bi-weekly report completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating bi-weekly report");
            throw;
        }
    }
}