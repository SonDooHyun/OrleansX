using Microsoft.Extensions.Hosting;
using OrleansX.Silo.Hosting;

Console.WriteLine("Starting Orleans Silo for Client SDK Tutorial...");

var builder = Host.CreateDefaultBuilder(args)
    .UseOrleans((context, siloBuilder) =>
    {
        siloBuilder.UseOrleansX(silo =>
        {
            silo.UseLocalhostClustering(siloPort: 11111, gatewayPort: 30000);
            silo.AddMemoryGrainStorage("user");
        });
    });

var host = builder.Build();

Console.WriteLine("Silo started successfully!");
Console.WriteLine("Press Ctrl+C to stop the silo.");

await host.RunAsync();
