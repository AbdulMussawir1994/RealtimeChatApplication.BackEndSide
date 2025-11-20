namespace App.Core.Interface.SemaphoreSlim;

public interface IReportService
{
    Task<string> GenerateReportAsync();
}
