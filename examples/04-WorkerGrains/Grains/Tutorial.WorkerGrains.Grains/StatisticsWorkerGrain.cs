using Microsoft.Extensions.Logging;
using Orleans.Runtime;
using OrleansX.Grains;

namespace Tutorial.WorkerGrains.Grains;

public class StatisticsWorkerGrain : StatefulWorkerGrainBase<StatisticsState>, IStatisticsWorkerGrain
{
    public StatisticsWorkerGrain(
        [PersistentState("statistics")] IPersistentState<StatisticsState> state,
        ILogger<StatisticsWorkerGrain> logger)
        : base(state, logger)
    {
    }

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        await base.OnActivateAsync(cancellationToken);
        await StartTimerAsync(TimeSpan.FromSeconds(10), TimeSpan.FromMinutes(5));
    }

    public Task StartAsync()
    {
        return StartTimerAsync(TimeSpan.Zero, TimeSpan.FromMinutes(5));
    }

    public new Task StopAsync()
    {
        return base.StopAsync();
    }

    public new Task<WorkerStatus> GetStatusAsync()
    {
        return base.GetStatusAsync();
    }

    public Task<StatisticsReport> GetReportAsync()
    {
        return Task.FromResult(new StatisticsReport
        {
            TotalProcessed = State.TotalProcessed,
            LastAggregationTime = State.LastAggregationTime,
            CountersByType = new Dictionary<string, long>(State.CountersByType),
            IsRunning = IsRunning
        });
    }

    protected override async Task ExecuteWorkAsync()
    {
        Logger.LogInformation("Aggregating statistics at {Time}", DateTime.UtcNow);

        var newStats = await CollectStatisticsAsync();

        State.TotalProcessed += newStats.Total;
        State.LastAggregationTime = DateTime.UtcNow;

        foreach (var (type, count) in newStats.ByType)
        {
            if (State.CountersByType.ContainsKey(type))
                State.CountersByType[type] += count;
            else
                State.CountersByType[type] = count;
        }

        await SaveStateAsync();

        Logger.LogInformation("Statistics aggregated. Total: {Total}", State.TotalProcessed);
    }

    private async Task<(long Total, Dictionary<string, long> ByType)> CollectStatisticsAsync()
    {
        await Task.Delay(100);

        return (
            Total: 100,
            ByType: new Dictionary<string, long>
            {
                ["Login"] = 45,
                ["Purchase"] = 30,
                ["Search"] = 25
            }
        );
    }
}
