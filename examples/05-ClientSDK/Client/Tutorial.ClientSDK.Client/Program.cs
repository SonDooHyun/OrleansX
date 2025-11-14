using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Orleans;
using OrleansX.Abstractions;
using OrleansX.Client;
using OrleansX.Client.Extensions;
using Tutorial.ClientSDK.Grains;

Console.WriteLine("Connecting to Orleans cluster...");

var builder = Host.CreateDefaultBuilder(args)
    .UseOrleansClient((context, clientBuilder) =>
    {
        clientBuilder.UseLocalhostClustering(gatewayPort: 30000);
    })
    .ConfigureServices(services =>
    {
        services.AddOrleansXClient();
        services.AddScoped<UserService>();
    });

var host = builder.Build();
await host.StartAsync();

Console.WriteLine("Connected to Orleans cluster!");
Console.WriteLine();

var service = host.Services.GetRequiredService<UserService>();
await service.TestUserOperationsAsync();

Console.WriteLine();
Console.WriteLine("Press any key to exit...");
Console.ReadKey();

await host.StopAsync();

public class UserService
{
    private readonly IGrainInvoker _invoker;

    public UserService(IGrainInvoker invoker)
    {
        _invoker = invoker;
    }

    public async Task TestUserOperationsAsync()
    {
        var user = _invoker.GetGrain<IUserGrain>("user-001");

        await user.UpdateNameAsync("John Doe");
        Console.WriteLine("User name updated");

        var info = await user.GetInfoAsync();
        Console.WriteLine($"User: {info.Name}");
        Console.WriteLine($"Updated: {info.UpdatedAt}");
    }
}
