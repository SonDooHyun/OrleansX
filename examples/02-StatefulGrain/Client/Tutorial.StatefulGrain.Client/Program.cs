using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Orleans;
using OrleansX.Abstractions;
using OrleansX.Client;
using OrleansX.Client.Extensions;
using Tutorial.StatefulGrain.Grains;

Console.WriteLine("Connecting to Orleans cluster...");

var builder = Host.CreateDefaultBuilder(args)
    .UseOrleansClient((context, clientBuilder) =>
    {
        clientBuilder.UseLocalhostClustering(gatewayPort: 30000);
    })
    .ConfigureServices(services =>
    {
        services.AddOrleansXClient();
        services.AddScoped<PlayerService>();
    });

var host = builder.Build();
await host.StartAsync();

Console.WriteLine("Connected to Orleans cluster!");
Console.WriteLine();

var service = host.Services.GetRequiredService<PlayerService>();
await service.PlayGameAsync("player-001");

Console.WriteLine();
Console.WriteLine("Press any key to exit...");
Console.ReadKey();

await host.StopAsync();

public class PlayerService
{
    private readonly IGrainInvoker _invoker;

    public PlayerService(IGrainInvoker invoker)
    {
        _invoker = invoker;
    }

    public async Task PlayGameAsync(string playerId)
    {
        var player = _invoker.GetGrain<IPlayerGrain>(playerId);

        // 플레이어 초기화
        await player.InitializeAsync("HeroPlayer");

        // 로그인 시간 업데이트
        await player.UpdateLastLoginAsync();

        // 게임 플레이 - 경험치와 골드 획득
        await player.AddExperienceAsync(50);
        await player.AddGoldAsync(100);

        await player.AddExperienceAsync(80);
        await player.AddGoldAsync(50);

        // 플레이어 정보 조회
        var info = await player.GetInfoAsync();
        Console.WriteLine($"Player: {info.Name}");
        Console.WriteLine($"Level: {info.Level}");
        Console.WriteLine($"Experience: {info.Experience}");
        Console.WriteLine($"Gold: {info.Gold}");
        Console.WriteLine($"Created: {info.CreatedAt}");
        Console.WriteLine($"Last Login: {info.LastLoginAt}");
    }
}
