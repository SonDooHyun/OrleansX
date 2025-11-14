# Tutorial 02: Stateful Grain

## 개요

Stateful Grain은 영속적인 상태를 가지는 Grain입니다. Orleans는 자동으로 상태를 저장하고 불러오며, 각 Grain 인스턴스는 고유한 상태를 유지합니다.

## 언제 사용하나요?

- 사용자 프로필 관리
- 게임 플레이어 상태
- 주문 및 거래 정보
- 세션 관리

## 예제: 플레이어 Grain

### 1. 상태 모델 정의

```csharp
using Orleans;

namespace OrleansX.Tutorials.StatefulGrain;

[GenerateSerializer]
public class PlayerState
{
    [Id(0)]
    public string Name { get; set; } = string.Empty;

    [Id(1)]
    public int Level { get; set; } = 1;

    [Id(2)]
    public int Experience { get; set; } = 0;

    [Id(3)]
    public int Gold { get; set; } = 100;

    [Id(4)]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Id(5)]
    public DateTime LastLoginAt { get; set; } = DateTime.UtcNow;
}
```

### 2. Grain 인터페이스 정의

```csharp
using Orleans;

namespace OrleansX.Tutorials.StatefulGrain;

public interface IPlayerGrain : IGrainWithStringKey
{
    Task<PlayerInfo> GetInfoAsync();
    Task InitializeAsync(string name);
    Task AddExperienceAsync(int amount);
    Task AddGoldAsync(int amount);
    Task UpdateLastLoginAsync();
}

[GenerateSerializer]
public class PlayerInfo
{
    [Id(0)]
    public string PlayerId { get; set; } = string.Empty;

    [Id(1)]
    public string Name { get; set; } = string.Empty;

    [Id(2)]
    public int Level { get; set; }

    [Id(3)]
    public int Experience { get; set; }

    [Id(4)]
    public int Gold { get; set; }

    [Id(5)]
    public DateTime CreatedAt { get; set; }

    [Id(6)]
    public DateTime LastLoginAt { get; set; }
}
```

### 3. Grain 구현

```csharp
using Microsoft.Extensions.Logging;
using Orleans.Runtime;
using OrleansX.Grains;

namespace OrleansX.Tutorials.StatefulGrain;

public class PlayerGrain : StatefulGrainBase<PlayerState>, IPlayerGrain
{
    public PlayerGrain(
        [PersistentState("player")] IPersistentState<PlayerState> state,
        ILogger<PlayerGrain> logger)
        : base(state, logger)
    {
    }

    public Task<PlayerInfo> GetInfoAsync()
    {
        return Task.FromResult(new PlayerInfo
        {
            PlayerId = this.GetPrimaryKeyString(),
            Name = State.Name,
            Level = State.Level,
            Experience = State.Experience,
            Gold = State.Gold,
            CreatedAt = State.CreatedAt,
            LastLoginAt = State.LastLoginAt
        });
    }

    public async Task InitializeAsync(string name)
    {
        if (!string.IsNullOrEmpty(State.Name))
        {
            Logger.LogWarning("Player {PlayerId} already initialized", this.GetPrimaryKeyString());
            return;
        }

        State.Name = name;
        State.CreatedAt = DateTime.UtcNow;
        State.LastLoginAt = DateTime.UtcNow;

        await SaveStateAsync();

        Logger.LogInformation("Player {PlayerId} initialized with name {Name}",
            this.GetPrimaryKeyString(), name);
    }

    public async Task AddExperienceAsync(int amount)
    {
        State.Experience += amount;

        // 레벨업 체크 (100 경험치당 1레벨)
        while (State.Experience >= State.Level * 100)
        {
            State.Experience -= State.Level * 100;
            State.Level++;
            Logger.LogInformation("Player {PlayerId} leveled up to {Level}",
                this.GetPrimaryKeyString(), State.Level);
        }

        await SaveStateAsync();
    }

    public async Task AddGoldAsync(int amount)
    {
        State.Gold += amount;
        await SaveStateAsync();

        Logger.LogInformation("Player {PlayerId} gained {Amount} gold. Total: {Total}",
            this.GetPrimaryKeyString(), amount, State.Gold);
    }

    public async Task UpdateLastLoginAsync()
    {
        State.LastLoginAt = DateTime.UtcNow;
        await SaveStateAsync();
    }
}
```

### 4. 클라이언트에서 사용

```csharp
using OrleansX.Abstractions;

namespace OrleansX.Tutorials.StatefulGrain;

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
```

## 실행 방법

### Silo 구성 (메모리 스토리지)

```csharp
var builder = Host.CreateDefaultBuilder(args)
    .UseOrleans((context, siloBuilder) =>
    {
        siloBuilder.UseOrleansX(options =>
        {
            options.UseLocalhostClustering(siloPort: 11111, gatewayPort: 30000);
            options.AddMemoryGrainStorage("player");
        });
    });

await builder.Build().RunAsync();
```

### Silo 구성 (SQL Server 스토리지)

```csharp
siloBuilder.UseOrleansX(options =>
{
    options.UseLocalhostClustering(siloPort: 11111, gatewayPort: 30000);
    options.AddAdoNetGrainStorage("player", opt =>
    {
        opt.ConnectionString = "Server=localhost;Database=Orleans;User Id=sa;Password=yourpassword;";
        opt.Invariant = "Microsoft.Data.SqlClient";
    });
});
```

## 주요 특징

### 1. 자동 상태 관리
- `State` 프로퍼티로 현재 상태 접근
- `SaveStateAsync()`로 상태 저장
- `ReadStateAsync()`로 상태 명시적 로드
- `ClearStateAsync()`로 상태 삭제

### 2. 상태 직렬화
- `[GenerateSerializer]`와 `[Id]` 속성 사용
- Orleans가 자동으로 직렬화 코드 생성

### 3. Etag 지원
- 낙관적 동시성 제어
- `StateEtag` 프로퍼티로 버전 확인

## 실행 예제

```bash
# Silo 실행
cd Tutorials/02-StatefulGrain/SiloHost
dotnet run

# 별도 터미널에서 클라이언트 실행
cd Tutorials/02-StatefulGrain/Client
dotnet run
```

## 예상 출력

```
Player: HeroPlayer
Level: 2
Experience: 30
Gold: 250
Created: 2025-01-15 10:30:00
Last Login: 2025-01-15 10:30:05
```

## Best Practices

### 1. 상태 변경 후 항상 저장
```csharp
// ✅ 좋은 예
State.Gold += 100;
await SaveStateAsync();

// ❌ 나쁜 예 - 저장 안 함
State.Gold += 100;
// 상태가 저장되지 않음!
```

### 2. 큰 상태는 분할 고려
```csharp
// 대신 여러 Grain으로 분리
// PlayerGrain - 기본 정보
// PlayerInventoryGrain - 인벤토리
// PlayerQuestGrain - 퀘스트
```

### 3. 읽기 전용 작업은 저장하지 않기
```csharp
public Task<PlayerInfo> GetInfoAsync()
{
    // 읽기만 하므로 SaveStateAsync() 불필요
    return Task.FromResult(MapToInfo());
}
```

## 다음 단계

- [Tutorial 03: Transactional Grain](../03-TransactionalGrain/README.md) - 트랜잭션 학습
- [Tutorial 04: Worker Grains](../04-WorkerGrains/README.md) - 백그라운드 작업 학습
