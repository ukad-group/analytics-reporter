using Google.Apis.Auth.OAuth2;
using Google.Apis.SearchConsole.v1;
using SearchConsoleAPIService = Google.Apis.SearchConsole.v1.SearchConsoleService;
using Google.Apis.SearchConsole.v1.Data;
using Google.Apis.Services;
using GSCReporter.Services.Configuration;
using GSCReporter.Services.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GSCReporter.Services.Services;

public class SearchConsoleService : ISearchConsoleService
{
    private readonly SearchConsoleConfig _config;
    private readonly ILogger<SearchConsoleService> _logger;
    private readonly SearchConsoleAPIService _apiService;

    public SearchConsoleService(IOptions<AppConfig> config, ILogger<SearchConsoleService> logger)
    {
        _config = config.Value.SearchConsole;
        _logger = logger;
        _apiService = InitializeService();
    }

    private SearchConsoleAPIService InitializeService()
    {
        GoogleCredential credential;
        
        // Try JSON content first (for Azure deployment), then fall back to file path (for local development)
        if (!string.IsNullOrEmpty(_config.ServiceAccountKeyJson))
        {
            credential = GoogleCredential.FromJson(_config.ServiceAccountKeyJson)
                .CreateScoped("https://www.googleapis.com/auth/webmasters.readonly");
        }
        else if (!string.IsNullOrEmpty(_config.ServiceAccountKeyPath))
        {
            credential = GoogleCredential.FromFile(_config.ServiceAccountKeyPath)
                .CreateScoped("https://www.googleapis.com/auth/webmasters.readonly");
        }
        else
        {
            throw new InvalidOperationException("Either ServiceAccountKeyJson or ServiceAccountKeyPath must be provided");
        }

        return new SearchConsoleAPIService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "GSC Reporter"
        });
    }

    public async Task<SearchConsoleReport> GetReportAsync(DateTime startDate, DateTime endDate)
    {
        try
        {
            _logger.LogInformation("Fetching Search Console data for {StartDate} - {EndDate}", 
                startDate.ToString("yyyy-MM-dd"), endDate.ToString("yyyy-MM-dd"));

            // Calculate previous period dates (same duration, ending the day before current period starts)
            var periodLength = (endDate - startDate).Days + 1;
            var previousEndDate = startDate.AddDays(-1);
            var previousStartDate = previousEndDate.AddDays(-(periodLength - 1));

            _logger.LogInformation("Fetching comparison data for {PrevStartDate} - {PrevEndDate}", 
                previousStartDate.ToString("yyyy-MM-dd"), previousEndDate.ToString("yyyy-MM-dd"));

            // Fetch current period data
            var currentData = await FetchPeriodDataAsync(startDate, endDate);
            
            // Fetch previous period data
            var previousData = await FetchPeriodDataAsync(previousStartDate, previousEndDate);

            // Merge data with comparison metrics
            var marketStatistics = MergeMarketStatistics(currentData.MarketData, previousData.MarketData);

            if (currentData.TotalData.Rows == null || !currentData.TotalData.Rows.Any())
            {
                _logger.LogWarning("No data returned from Search Console API for current period");
                return new SearchConsoleReport
                {
                    StartDate = startDate,
                    EndDate = endDate,
                    MarketStatistics = marketStatistics
                };
            }

            var firstRow = currentData.TotalData.Rows.First();
            var totalClicks = (long)(firstRow.Clicks ?? 0);
            var totalImpressions = (long)(firstRow.Impressions ?? 0);
            var averageCTR = firstRow.Ctr ?? 0;
            var averagePosition = firstRow.Position ?? 0;

            _logger.LogInformation("Retrieved data: {Clicks} clicks, {Impressions} impressions, {Markets} markets", 
                totalClicks, totalImpressions, marketStatistics.Count);

            return new SearchConsoleReport
            {
                StartDate = startDate,
                EndDate = endDate,
                TotalClicks = totalClicks,
                TotalImpressions = totalImpressions,
                AverageCTR = averageCTR,
                AveragePosition = averagePosition,
                MarketStatistics = marketStatistics
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching Search Console data");
            throw;
        }
    }

    private async Task<(SearchAnalyticsQueryResponse TotalData, SearchAnalyticsQueryResponse MarketData)> FetchPeriodDataAsync(DateTime startDate, DateTime endDate)
    {
        var request = new SearchAnalyticsQueryRequest
        {
            StartDate = startDate.ToString("yyyy-MM-dd"),
            EndDate = endDate.ToString("yyyy-MM-dd"),
            SearchType = "web",  // Explicitly set to web search (default in GSC UI)
            RowLimit = 25000
        };

        var query = _apiService.Searchanalytics.Query(request, _config.SiteUrl);
        var totalResponse = await query.ExecuteAsync();

        // Get market-specific data by country
        var countryRequest = new SearchAnalyticsQueryRequest
        {
            StartDate = startDate.ToString("yyyy-MM-dd"),
            EndDate = endDate.ToString("yyyy-MM-dd"),
            SearchType = "web",  // Explicitly set to web search (default in GSC UI)
            Dimensions = new[] { "country" },
            RowLimit = 25000
        };

        var countryQuery = _apiService.Searchanalytics.Query(countryRequest, _config.SiteUrl);
        var marketResponse = await countryQuery.ExecuteAsync();

        return (totalResponse, marketResponse);
    }

    private List<MarketStatistics> MergeMarketStatistics(SearchAnalyticsQueryResponse currentData, SearchAnalyticsQueryResponse previousData)
    {
        var currentMarkets = new Dictionary<string, MarketStatistics>();
        var previousMarkets = new Dictionary<string, MarketStatistics>();

        // Process current period data
        if (currentData.Rows != null && currentData.Rows.Any())
        {
            foreach (var row in currentData.Rows.Where(r => r.Keys != null && r.Keys.Any()))
            {
                var country = row.Keys.First();
                currentMarkets[country] = new MarketStatistics
                {
                    Country = country,
                    TotalClicks = (long)(row.Clicks ?? 0),
                    TotalImpressions = (long)(row.Impressions ?? 0),
                    AverageCTR = row.Ctr ?? 0,
                    AveragePosition = row.Position ?? 0
                };
            }
        }

        // Process previous period data
        if (previousData.Rows != null && previousData.Rows.Any())
        {
            foreach (var row in previousData.Rows.Where(r => r.Keys != null && r.Keys.Any()))
            {
                var country = row.Keys.First();
                previousMarkets[country] = new MarketStatistics
                {
                    Country = country,
                    TotalClicks = (long)(row.Clicks ?? 0),
                    TotalImpressions = (long)(row.Impressions ?? 0),
                    AverageCTR = row.Ctr ?? 0,
                    AveragePosition = row.Position ?? 0
                };
            }
        }

        // Merge data with comparison metrics
        var allCountries = currentMarkets.Keys.Union(previousMarkets.Keys).ToList();
        var mergedStatistics = new List<MarketStatistics>();

        foreach (var country in allCountries)
        {
            var current = currentMarkets.GetValueOrDefault(country) ?? new MarketStatistics { Country = country };
            var previous = previousMarkets.GetValueOrDefault(country) ?? new MarketStatistics { Country = country };

            mergedStatistics.Add(new MarketStatistics
            {
                Country = current.Country,
                TotalClicks = current.TotalClicks,
                TotalImpressions = current.TotalImpressions,
                AverageCTR = current.AverageCTR,
                AveragePosition = current.AveragePosition,
                PreviousTotalClicks = previous.TotalClicks,
                PreviousTotalImpressions = previous.TotalImpressions,
                PreviousAverageCTR = previous.AverageCTR
            });
        }

        return mergedStatistics.OrderByDescending(m => m.TotalClicks).ToList();
    }
}