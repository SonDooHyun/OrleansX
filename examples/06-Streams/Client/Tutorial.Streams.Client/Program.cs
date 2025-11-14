using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Orleans;
using OrleansX.Abstractions;
using OrleansX.Client;
using OrleansX.Client.Extensions;
using Tutorial.Streams.Grains;

Console.WriteLine("Connecting to Orleans cluster...");

var builder = Host.CreateDefaultBuilder(args)
    .UseOrleansClient((context, clientBuilder) =>
    {
        clientBuilder.UseLocalhostClustering(gatewayPort: 30000);
    })
    .ConfigureServices(services =>
    {
        services.AddOrleansXClient();
        services.AddScoped<NotificationService>();
    });

var host = builder.Build();
await host.StartAsync();

Console.WriteLine("Connected to Orleans cluster!");
Console.WriteLine();

var service = host.Services.GetRequiredService<NotificationService>();
await service.DemoStreamingAsync();

Console.WriteLine();
Console.WriteLine("Press any key to exit...");
Console.ReadKey();

await host.StopAsync();

public class NotificationService
{
    private readonly IGrainInvoker _invoker;

    public NotificationService(IGrainInvoker invoker)
    {
        _invoker = invoker;
    }

    public async Task DemoStreamingAsync()
    {
        var streamId = "user-notifications";

        var consumer = _invoker.GetGrain<INotificationConsumerGrain>(streamId);
        await consumer.StartListeningAsync();
        Console.WriteLine("Consumer started listening...");

        var producer = _invoker.GetGrain<INotificationProducerGrain>(streamId);

        await producer.SendNotificationAsync("user-123", "Welcome to OrleansX!", NotificationType.Info);
        await producer.SendNotificationAsync("user-123", "Your order has been shipped", NotificationType.Success);
        await producer.SendNotificationAsync("user-123", "Payment failed", NotificationType.Error);

        await Task.Delay(2000);

        var received = await consumer.GetReceivedNotificationsAsync();
        Console.WriteLine($"\nReceived {received.Count} notifications:");
        foreach (var notification in received)
        {
            Console.WriteLine($"  [{notification.Type}] {notification.Message}");
        }

        await consumer.StopListeningAsync();
    }
}
