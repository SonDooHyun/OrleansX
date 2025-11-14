using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Orleans;
using OrleansX.Abstractions;
using OrleansX.Client;
using OrleansX.Client.Extensions;
using Tutorial.StatelessGrain.Grains;

Console.WriteLine("Connecting to Orleans cluster...");

var builder = Host.CreateDefaultBuilder(args)
    .UseOrleansClient((context, clientBuilder) =>
    {
        clientBuilder.UseLocalhostClustering(gatewayPort: 30000);
    })
    .ConfigureServices(services =>
    {
        services.AddOrleansXClient();
        services.AddScoped<CalculatorService>();
    });

var host = builder.Build();
await host.StartAsync();

Console.WriteLine("Connected to Orleans cluster!");
Console.WriteLine();

var service = host.Services.GetRequiredService<CalculatorService>();
await service.PerformCalculationAsync();

Console.WriteLine();
Console.WriteLine("Press any key to exit...");
Console.ReadKey();

await host.StopAsync();

public class CalculatorService
{
    private readonly IGrainInvoker _invoker;

    public CalculatorService(IGrainInvoker invoker)
    {
        _invoker = invoker;
    }

    public async Task<int> PerformCalculationAsync()
    {
        var calculator = _invoker.GetGrain<ICalculatorGrain>("calculator");

        var sum = await calculator.AddAsync(10, 20);
        Console.WriteLine($"10 + 20 = {sum}");

        var product = await calculator.MultiplyAsync(5, 6);
        Console.WriteLine($"5 * 6 = {product}");

        var quotient = await calculator.DivideAsync(100, 4);
        Console.WriteLine($"100 / 4 = {quotient}");

        return sum;
    }
}
