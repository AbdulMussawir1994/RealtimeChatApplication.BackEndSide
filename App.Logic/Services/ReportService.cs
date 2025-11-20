using App.Core.Interface.SemaphoreSlim;

namespace App.Logic.Services;

public class ReportService : IReportService
{
    private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(2, 2);
    // Allow max 2 concurrent requests

    public async Task<string> GenerateReportAsync()
    {
        await _semaphore.WaitAsync(); // Wait for a free slot

        try
        {
            // Critical section
            await Task.Delay(3000); // Simulated long work
            return $"Report generated at {DateTime.Now}";
        }
        finally
        {
            _semaphore.Release();
        }
    }
}