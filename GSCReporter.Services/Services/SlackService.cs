using GSCReporter.Services.Configuration;
using GSCReporter.Services.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Slack.Webhooks;

namespace GSCReporter.Services.Services;

public class SlackService : ISlackService
{
    private readonly SlackConfig _config;
    private readonly ILogger<SlackService> _logger;
    private readonly SlackClient _slackClient;

    private const string ChatGptSourceName = "ChatGPT";

    public SlackService(IOptions<AppConfig> config, ILogger<SlackService> logger)
    {
        _config = config.Value.Slack;
        _logger = logger;
        _slackClient = new SlackClient(_config.WebhookUrl);
    }

    public async Task SendReportAsync(AggregatedReport report)
    {
        try
        {
            _logger.LogInformation("Sending report to Slack for period {StartDate} - {EndDate}",
                report.StartDate.ToString("dd.MM.yyyy"), report.EndDate.ToString("dd.MM.yyyy"));

            var message = FormatReportMessage(report);

            var slackMessage = new SlackMessage
            {
                Channel = _config.Channel,
                Text = message,
                Username = "GSC Reporter"
            };

            var result = await _slackClient.PostAsync(slackMessage);

            if (result)
            {
                _logger.LogInformation("Report sent successfully to Slack");
            }
            else
            {
                _logger.LogError("Failed to send report to Slack");
                throw new Exception("Failed to send message to Slack");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending report to Slack");
            throw;
        }
    }

    private string FormatReportMessage(AggregatedReport report)
    {
        var periodText = $"{report.StartDate:dd.MM.yyyy} - {report.EndDate:dd.MM.yyyy}";

        var message = $"📊 *Performance Report*\n\n{periodText}\n";

        // Add summary if GSC data available
        if (report.SearchConsole != null)
        {
            var clicksText = FormatNumber(report.SearchConsole.TotalClicks);
            var impressionsText = FormatNumber(report.SearchConsole.TotalImpressions);

            var targetCountries = TargetMarkets.Countries;
            var targetMarkets = report.SearchConsole.MarketStatistics
                .Where(m => targetCountries.ContainsKey(m.Country.ToLower()))
                .ToList();
            var marketClicks = targetMarkets.Sum(m => m.TotalClicks);
            var marketImpressions = targetMarkets.Sum(m => m.TotalImpressions);
            var marketCTR = marketImpressions > 0 ? (double)marketClicks / marketImpressions : 0;
            var marketPosition = marketImpressions > 0 ?
                targetMarkets.Sum(m => m.AveragePosition * m.TotalImpressions) / marketImpressions : 0;

            message += $@"
{clicksText} - clicks
{impressionsText} - impressions

Target markets = {string.Join(", ", targetCountries.Values)}
Average CTR by target markets: {marketCTR:P2}
Average position in target markets: {marketPosition:F1}";
        }

        // Create merged table with GSC + AI traffic data
        message += FormatMergedTable(report);

        return message;
    }

    private string FormatMergedTable(AggregatedReport report)
    {
        var targetCountries = TargetMarkets.Countries;
        var gscReport = report.SearchConsole;
        var paidAds = report.PaidAds;
        var aiTraffic = report.AITraffic;

        var message = "\n\n🌍 *Markets Performance (vs Previous Period):*\n```\n";

        message += $"{"Country",-11} | {"Organic",-35} | {"Ads",-38} | {"AI",-9}\n";
        message += $"{"",-11} | {"Clicks",-11} | {"Impr",-13} | {"CTR",-5} | {"Sessions",-11} | {"Eng.sessions",-13} | {"Eng.rate",-8} | {"ChatGPT",-9}\n";
        message += "------------|-------------|---------------|-------|-------------|---------------|----------|-----------\n";

        var rows = new List<(string Country, MarketStatistics? Organic, PaidAdsStatistics Ads, bool IsOther)>();

        foreach (var (countryCode, countryName) in targetCountries)
        {
            var market = gscReport?.MarketStatistics.FirstOrDefault(m =>
                string.Equals(m.Country, countryCode, StringComparison.OrdinalIgnoreCase));
            var ads = paidAds?.GetStatistics(countryName) ?? new PaidAdsStatistics { Country = countryName };

            rows.Add((countryName, market == null ? null : new MarketStatistics
            {
                Country = countryName,
                TotalClicks = market.TotalClicks,
                TotalImpressions = market.TotalImpressions,
                AverageCTR = market.AverageCTR,
                AveragePosition = market.AveragePosition,
                PreviousTotalClicks = market.PreviousTotalClicks,
                PreviousTotalImpressions = market.PreviousTotalImpressions,
                PreviousAverageCTR = market.PreviousAverageCTR
            }, ads, false));
        }

        var otherAds = paidAds?.GetStatistics("Other") ?? new PaidAdsStatistics { Country = "Other" };

        if (gscReport != null)
        {
            var otherMarkets = gscReport.MarketStatistics.Where(m =>
                !targetCountries.ContainsKey(m.Country.ToLower())).ToList();

            if (otherMarkets.Count > 0 || otherAds.Sessions > 0 || otherAds.EngagedSessions > 0)
            {
                rows.Add(("Other", new MarketStatistics
                {
                    Country = "Other",
                    TotalClicks = otherMarkets.Sum(m => m.TotalClicks),
                    TotalImpressions = otherMarkets.Sum(m => m.TotalImpressions),
                    AverageCTR = otherMarkets.Sum(m => m.TotalImpressions) == 0
                        ? 0
                        : (double)otherMarkets.Sum(m => m.TotalClicks) / otherMarkets.Sum(m => m.TotalImpressions),
                    PreviousTotalClicks = otherMarkets.Sum(m => m.PreviousTotalClicks),
                    PreviousTotalImpressions = otherMarkets.Sum(m => m.PreviousTotalImpressions)
                }, otherAds, true));
            }
        }
        else if (otherAds.Sessions > 0 || otherAds.EngagedSessions > 0)
        {
            rows.Add(("Other", null, otherAds, true));
        }

        foreach (var row in rows.OrderBy(r => r.IsOther ? 1 : 0)
                                .ThenByDescending(r => (r.Organic?.TotalClicks ?? 0) + r.Ads.Sessions))
        {
            var organic = row.Organic;
            var countryName = row.Country;

            var organicClicks = organic != null ? FormatNumberWithChange(organic.TotalClicks, organic.ClicksChangePercent) : "-";
            var organicImpr = organic != null ? FormatNumberWithChange(organic.TotalImpressions, organic.ImpressionsChangePercent) : "-";
            var organicCtr = organic != null ? $"{organic.AverageCTR:P1}" : "-";

            var adsSessions = FormatNumberWithChange(row.Ads.Sessions, row.Ads.SessionsChangePercent);
            var adsEngagedSessions = FormatNumberWithChange(row.Ads.EngagedSessions, row.Ads.EngagedSessionsChangePercent);
            var adsEngagementRate = $"{row.Ads.EngagementRate:P1}";

            var chatGptSessions = aiTraffic?.GetSessions(countryName, ChatGptSourceName) ?? 0;
            var previousChatGptSessions = aiTraffic?.GetPreviousSessions(countryName, ChatGptSourceName) ?? 0;
            var chatGptChange = previousChatGptSessions == 0
                ? 0
                : ((double)(chatGptSessions - previousChatGptSessions) / previousChatGptSessions) * 100;
            var chatGptText = chatGptSessions > 0 || previousChatGptSessions > 0
                ? FormatNumberWithChange(chatGptSessions, chatGptChange)
                : "-";

            var rowText = $"{countryName,-11} | {organicClicks,-11} | {organicImpr,-13} | {organicCtr,-5} | {adsSessions,-11} | {adsEngagedSessions,-13} | {adsEngagementRate,-8} | {chatGptText,-9}";

            message += rowText + "\n";
        }

        message += "```";
        return message;
    }

    private string FormatNumber(long number)
    {
        return number switch
        {
            >= 1_000_000 => $"{number / 1_000_000.0:F1}M",
            >= 1_000 => $"{number / 1_000.0:F1}K",
            _ => number.ToString()
        };
    }

    private string FormatPercentageChange(double changePercent)
    {
        if (Math.Abs(changePercent) < 0.1)
            return "0%";

        var sign = changePercent > 0 ? "+" : "";
        return $"{sign}{changePercent:F0}%";
    }

    private string FormatNumberWithChange(long number, double changePercent)
    {
        var numberText = FormatNumber(number);
        var changeText = FormatPercentageChange(changePercent);
        return $"{numberText} ({changeText})";
    }
}
