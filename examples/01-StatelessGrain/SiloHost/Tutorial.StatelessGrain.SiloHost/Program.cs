using Microsoft.Extensions.Hosting;
using OrleansX.Silo.Hosting;

Console.WriteLine("Starting Orleans Silo...");

var builder = Host.CreateDefaultBuilder(args)
    .UseOrleans((context, siloBuilder) =>
    {
        siloBuilder.UseOrleansX(silo =>
        {
            silo.UseLocalhostClustering(siloPort: 11111, gatewayPort: 30000);
        });
    });

var host = builder.Build();

Console.WriteLine("Silo started successfully!");
Console.WriteLine("Press Ctrl+C to stop the silo.");

await host.RunAsync();
