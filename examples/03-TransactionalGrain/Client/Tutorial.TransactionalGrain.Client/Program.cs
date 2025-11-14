using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Orleans;
using OrleansX.Abstractions;
using OrleansX.Client;
using OrleansX.Client.Extensions;
using Tutorial.TransactionalGrain.Grains;

Console.WriteLine("Connecting to Orleans cluster...");

var builder = Host.CreateDefaultBuilder(args)
    .UseOrleansClient((context, clientBuilder) =>
    {
        clientBuilder.UseLocalhostClustering(gatewayPort: 30000);
    })
    .ConfigureServices(services =>
    {
        services.AddOrleansXClient();
        services.AddScoped<BankingService>();
    });

var host = builder.Build();
await host.StartAsync();

Console.WriteLine("Connected to Orleans cluster!");
Console.WriteLine();

var service = host.Services.GetRequiredService<BankingService>();
await service.DemoTransferAsync();

Console.WriteLine();
Console.WriteLine("Press any key to exit...");
Console.ReadKey();

await host.StopAsync();

public class BankingService
{
    private readonly IGrainInvoker _invoker;

    public BankingService(IGrainInvoker invoker)
    {
        _invoker = invoker;
    }

    public async Task DemoTransferAsync()
    {
        var account1 = _invoker.GetGrain<IAccountGrain>("account-001");
        var account2 = _invoker.GetGrain<IAccountGrain>("account-002");

        // 초기 잔액 설정
        await account1.DepositAsync(1000m);
        await account2.DepositAsync(500m);

        Console.WriteLine($"Account 1 Balance: {await account1.GetBalanceAsync()}");
        Console.WriteLine($"Account 2 Balance: {await account2.GetBalanceAsync()}");
        Console.WriteLine();

        // 이체 실행
        try
        {
            Console.WriteLine("Transferring 300 from Account 1 to Account 2...");
            await account1.TransferToAsync("account-002", 300m);
            Console.WriteLine("Transfer successful!");
            Console.WriteLine();

            Console.WriteLine($"Account 1 Balance: {await account1.GetBalanceAsync()}");
            Console.WriteLine($"Account 2 Balance: {await account2.GetBalanceAsync()}");
            Console.WriteLine();
        }
        catch (InsufficientFundsException ex)
        {
            Console.WriteLine($"Transfer failed: {ex.Message}");
        }

        // 잔액 부족 이체 시도
        try
        {
            Console.WriteLine("Attempting to transfer 2000 from Account 1 to Account 2...");
            await account1.TransferToAsync("account-002", 2000m);
        }
        catch (InsufficientFundsException ex)
        {
            Console.WriteLine($"Transfer failed as expected: {ex.Message}");
            Console.WriteLine();

            // 롤백 확인
            Console.WriteLine($"Account 1 Balance (after rollback): {await account1.GetBalanceAsync()}");
            Console.WriteLine($"Account 2 Balance (after rollback): {await account2.GetBalanceAsync()}");
        }
    }
}
