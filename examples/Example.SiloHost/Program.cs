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
        opts.WithCluster("game-cluster", "game-service")
            .WithPorts(siloPort: 11111, gatewayPort: 30000)
            .WithClustering(new ClusteringOptions.Localhost())
            .WithPersistence(new PersistenceOptions.Memory())
            .WithStreams(new StreamsOptions.Memory("Default"))
            .WithTransactions(new TransactionOptions.Memory()); // 트랜잭션 지원
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
Console.WriteLine("OrleansX Game Matchmaking - Silo Host");
Console.WriteLine("Features: Player, Party, Matchmaking, Room, Transaction");
Console.WriteLine("=".PadRight(80, '='));
Console.WriteLine();
Console.WriteLine("⚙️  Cluster ID: game-cluster");
Console.WriteLine("⚙️  Service ID: game-service");
Console.WriteLine("⚙️  Silo Port: 11111");
Console.WriteLine("⚙️  Gateway Port: 30000");
Console.WriteLine("⚙️  Transaction: Enabled (Memory)");
Console.WriteLine();
Console.WriteLine("Starting Orleans Silo...");
Console.WriteLine();

await host.RunAsync();
