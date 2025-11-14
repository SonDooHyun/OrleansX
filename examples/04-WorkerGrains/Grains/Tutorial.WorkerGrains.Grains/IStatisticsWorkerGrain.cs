using Orleans;
using OrleansX.Grains;

namespace Tutorial.WorkerGrains.Grains;

public interface IStatisticsWorkerGrain : IGrainWithStringKey
{
    Task StartAsync();
    Task StopAsync();
    Task<WorkerStatus> GetStatusAsync();
    Task<StatisticsReport> GetReportAsync();
}
