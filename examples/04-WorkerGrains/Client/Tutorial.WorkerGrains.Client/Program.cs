using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Orleans;
using OrleansX.Abstractions;
using OrleansX.Client;
using OrleansX.Client.Extensions;
using Tutorial.WorkerGrains.Grains;

Console.WriteLine("Connecting to Orleans cluster...");

var builder = Host.CreateDefaultBuilder(args)
    .UseOrleansClient((context, clientBuilder) =>
    {
        clientBuilder.UseLocalhostClustering(gatewayPort: 30000);
    })
    .ConfigureServices(services =>
    {
        services.AddOrleansXClient();
        services.AddScoped<WorkerManagementService>();
    });

var host = builder.Build();
await host.StartAsync();

Console.WriteLine("Connected to Orleans cluster!");
Console.WriteLine();

var service = host.Services.GetRequiredService<WorkerManagementService>();
await service.ManageCleanupWorkerAsync();
Console.WriteLine();
await service.MonitorStatisticsWorkerAsync();

Console.WriteLine();
Console.WriteLine("Press any key to exit...");
Console.ReadKey();

await host.StopAsync();

public class WorkerManagementService
{
    private readonly IGrainInvoker _invoker;

    public WorkerManagementService(IGrainInvoker invoker)
    {
        _invoker = invoker;
    }

    public async Task ManageCleanupWorkerAsync()
    {
        var worker = _invoker.GetGrain<ICleanupWorkerGrain>("cleanup-worker");

        await worker.StartAsync();
        Console.WriteLine("Cleanup worker started");

        await Task.Delay(5000);
        var status = await worker.GetStatusAsync();
        Console.WriteLine($"Worker Status:");
        Console.WriteLine($"  IsRunning: {status.IsRunning}");
        Console.WriteLine($"  Success Count: {status.SuccessCount}");
        Console.WriteLine($"  Failure Count: {status.FailureCount}");
        Console.WriteLine($"  Last Execution: {status.LastExecutionTime}");
        Console.WriteLine($"  Next Execution: {status.NextExecutionTime}");

        await worker.StopAsync();
        Console.WriteLine("Cleanup worker stopped");
    }

    public async Task MonitorStatisticsWorkerAsync()
    {
        var worker = _invoker.GetGrain<IStatisticsWorkerGrain>("stats-worker");

        var report = await worker.GetReportAsync();
        Console.WriteLine($"Statistics Report:");
        Console.WriteLine($"  Total Processed: {report.TotalProcessed}");
        Console.WriteLine($"  Last Aggregation: {report.LastAggregationTime}");
        Console.WriteLine($"  Is Running: {report.IsRunning}");
        Console.WriteLine($"  Counters:");
        foreach (var (type, count) in report.CountersByType)
        {
            Console.WriteLine($"    {type}: {count}");
        }
    }
}
