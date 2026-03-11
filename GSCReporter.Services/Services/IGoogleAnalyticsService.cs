using GSCReporter.Services.Models;

namespace GSCReporter.Services.Services;

public interface IGoogleAnalyticsService
{
    Task<AITrafficReport> GetAITrafficReportAsync(DateTime startDate, DateTime endDate);
    Task<PaidAdsReport> GetPaidAdsReportAsync(DateTime startDate, DateTime endDate);
}
