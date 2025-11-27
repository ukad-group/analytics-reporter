using GSCReporter.Services.Models;

namespace GSCReporter.Services.Services;

public interface ISearchConsoleService
{
    Task<SearchConsoleReport> GetReportAsync(DateTime startDate, DateTime endDate);
}