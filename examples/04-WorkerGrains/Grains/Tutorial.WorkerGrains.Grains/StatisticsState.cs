using Orleans;

namespace Tutorial.WorkerGrains.Grains;

[GenerateSerializer]
public class StatisticsState
{
    [Id(0)]
    public long TotalProcessed { get; set; }

    [Id(1)]
    public DateTime LastAggregationTime { get; set; }

    [Id(2)]
    public Dictionary<string, long> CountersByType { get; set; } = new();
}

[GenerateSerializer]
public class StatisticsReport
{
    [Id(0)]
    public long TotalProcessed { get; set; }

    [Id(1)]
    public DateTime LastAggregationTime { get; set; }

    [Id(2)]
    public Dictionary<string, long> CountersByType { get; set; } = new();

    [Id(3)]
    public bool IsRunning { get; set; }
}
