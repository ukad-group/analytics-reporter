namespace GSCReporter.Services.Services;

public interface IReportService
{
    Task GenerateAndSendBiWeeklyReportAsync();
}