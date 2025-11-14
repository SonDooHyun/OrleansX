using Microsoft.Extensions.Logging;
using OrleansX.Grains;

namespace Tutorial.WorkerGrains.Grains;

public class CleanupWorkerGrain : StatelessWorkerGrainBase, ICleanupWorkerGrain
{
    public CleanupWorkerGrain(ILogger<CleanupWorkerGrain> logger)
        : base(logger)
    {
    }

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        await base.OnActivateAsync(cancellationToken);
        await StartTimerAsync(TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(30));
    }

    public Task StartAsync()
    {
        return StartTimerAsync(TimeSpan.Zero, TimeSpan.FromMinutes(30));
    }

    public new Task StopAsync()
    {
        return base.StopAsync();
    }

    public new Task<WorkerStatus> GetStatusAsync()
    {
        return base.GetStatusAsync();
    }

    protected override async Task ExecuteWorkAsync()
    {
        Logger.LogInformation("Starting cleanup work at {Time}", DateTime.UtcNow);

        await CleanupExpiredSessionsAsync();
        await CleanupOldLogsAsync();
        await CleanupTempFilesAsync();

        Logger.LogInformation("Cleanup work completed at {Time}", DateTime.UtcNow);
    }

    private async Task CleanupExpiredSessionsAsync()
    {
        await Task.Delay(100);
        Logger.LogInformation("Expired sessions cleaned up");
    }

    private async Task CleanupOldLogsAsync()
    {
        await Task.Delay(100);
        Logger.LogInformation("Old logs cleaned up");
    }

    private async Task CleanupTempFilesAsync()
    {
        await Task.Delay(100);
        Logger.LogInformation("Temp files cleaned up");
    }

    protected override Task OnErrorAsync(Exception exception)
    {
        Logger.LogError(exception, "Error occurred during cleanup work");
        return Task.CompletedTask;
    }
}
