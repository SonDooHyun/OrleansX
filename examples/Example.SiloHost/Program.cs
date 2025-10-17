using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Orleans.Hosting;
using OrleansX.Abstractions.Options;
using OrleansX.Silo.Hosting.Extensions;

var builder = Host.CreateDefaultBuilder(args);

builder.UseOrleans((context, siloBuilder) =>
{
    siloBuilder.UseOrleansXDefaults(opts =>
    {
        opts.WithCluster("game-cluster", "party-service")
            .WithPorts(siloPort: 11111, gatewayPort: 30000)
            .WithClustering(new ClusteringOptions.Localhost())
            .WithPersistence(new PersistenceOptions.Memory())
            .WithStreams(new StreamsOptions.Memory("Default"));
    });

    // Grain 어셈블리는 자동으로 감지됩니다 (Orleans 9.x)
});

builder.ConfigureLogging(logging =>
{
    logging.ClearProviders();
    logging.AddConsole();
    logging.SetMinimumLevel(LogLevel.Information);
});

var host = builder.Build();

Console.WriteLine("=".PadRight(80, '='));
Console.WriteLine("OrleansX Example - Silo Host");
Console.WriteLine("Game Party & Matchmaking System");
Console.WriteLine("=".PadRight(80, '='));
Console.WriteLine();
Console.WriteLine("Starting Orleans Silo...");
Console.WriteLine();

await host.RunAsync();
