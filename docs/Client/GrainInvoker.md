# GrainInvoker 심층 가이드

## 📖 개요

`GrainInvoker`는 OrleansX의 핵심 클라이언트 컴포넌트로, **Grain 참조를 가져오는 작업을 단순화하고 표준화**합니다.

**핵심 역할**: Orleans의 `IClusterClient`에 대한 **Facade(파사드)** 패턴 구현

## 🎯 왜 GrainInvoker를 만들었나?

### 문제점: IClusterClient 직접 사용

```csharp
// ❌ IClusterClient를 직접 주입받으면...
public class PlayerController : ControllerBase
{
    private readonly IClusterClient _client;

    public PlayerController(IClusterClient client)
    {
        _client = client;
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetPlayer(string id)
    {
        // Orleans 구현에 강하게 결합됨
        var player = _client.GetGrain<IPlayerGrain>(id);
        var data = await player.GetDataAsync();
        return Ok(data);
    }
}
```

**문제점:**
1. **Orleans에 강한 결합**: 코드가 Orleans의 `IClusterClient`에 직접 의존
2. **테스트 어려움**: `IClusterClient`를 Mock하기 복잡함
3. **검증 누락**: 빈 키, null 검증 등을 매번 직접 해야 함
4. **확장성 부족**: 공통 로직(로깅, 재시도 등) 추가가 어려움

### 해결책: IGrainInvoker 인터페이스

```csharp
// ✅ IGrainInvoker를 주입받으면...
public class PlayerController : ControllerBase
{
    private readonly IGrainInvoker _invoker;

    public PlayerController(IGrainInvoker invoker)
    {
        _invoker = invoker;  // OrleansX 추상화에 의존
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetPlayer(string id)
    {
        // 단순하고 명확한 API
        var player = _invoker.GetGrain<IPlayerGrain>(id);
        var data = await player.GetDataAsync();
        return Ok(data);
    }
}
```

**장점:**
1. **느슨한 결합**: Orleans 구현 세부사항 숨김
2. **테스트 용이**: `IGrainInvoker` Mock이 간단함
3. **자동 검증**: 빈 키, null 등 자동 검증
4. **확장 가능**: 인터페이스 뒤에서 기능 추가 가능

## 🔍 GrainInvoker 내부 구조

### 파사드 패턴 구현

```csharp
// OrleansX.Client/GrainInvoker.cs
public class GrainInvoker : IGrainInvoker
{
    private readonly IClusterClient _client;

    public GrainInvoker(IClusterClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    // String 키
    public TGrain GetGrain<TGrain>(string key) where TGrain : IGrainWithStringKey
    {
        // 검증 추가
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Key cannot be null or empty", nameof(key));

        // IClusterClient에 위임
        return _client.GetGrain<TGrain>(key);
    }

    // Guid 키
    public TGrain GetGrain<TGrain>(Guid key) where TGrain : IGrainWithGuidKey
    {
        if (key == Guid.Empty)
            throw new ArgumentException("Key cannot be empty", nameof(key));

        return _client.GetGrain<TGrain>(key);
    }

    // Integer 키
    public TGrain GetGrain<TGrain>(long key) where TGrain : IGrainWithIntegerKey
    {
        return _client.GetGrain<TGrain>(key);
    }

    // 복합 키 (Guid + String)
    public TGrain GetGrain<TGrain>(Guid key, string keyExtension)
        where TGrain : IGrainWithGuidCompoundKey
    {
        if (key == Guid.Empty)
            throw new ArgumentException("Key cannot be empty", nameof(key));
        if (string.IsNullOrWhiteSpace(keyExtension))
            throw new ArgumentException("Key extension cannot be null or empty", nameof(keyExtension));

        return _client.GetGrain<TGrain>(key, keyExtension);
    }
}
```

### 의존성 주입 등록

```csharp
// OrleansX.Client/Extensions/ServiceCollectionExtensions.cs (79번 라인)
public static IServiceCollection AddOrleansXClient(
    this IServiceCollection services,
    OrleansClientOptions options)
{
    // ... Orleans Client 설정 ...

    // ✅ IGrainInvoker를 Singleton으로 등록
    services.AddSingleton<IGrainInvoker, GrainInvoker>();

    return services;
}
```

## 💻 실전 사용 예제

### 1. Controller에서 사용

```csharp
[ApiController]
[Route("api/players")]
public class PlayerController : ControllerBase
{
    private readonly IGrainInvoker _invoker;
    private readonly ILogger<PlayerController> _logger;

    public PlayerController(
        IGrainInvoker invoker,
        ILogger<PlayerController> logger)
    {
        _invoker = invoker;
        _logger = logger;
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetPlayer(string id)
    {
        var player = _invoker.GetGrain<IPlayerGrain>(id);
        var data = await player.GetDataAsync();
        return Ok(data);
    }

    [HttpPost("{id}/level-up")]
    public async Task<IActionResult> LevelUp(string id)
    {
        var player = _invoker.GetGrain<IPlayerGrain>(id);
        await player.LevelUpAsync();

        _logger.LogInformation("Player {PlayerId} leveled up", id);
        return Ok();
    }

    [HttpPost("{id}/buy")]
    public async Task<IActionResult> BuyItem(string id, [FromBody] BuyItemRequest request)
    {
        var player = _invoker.GetGrain<IPlayerGrain>(id);
        var result = await player.BuyItemAsync(request.ItemId, request.Quantity);

        if (result.Success)
            return Ok(result);
        else
            return BadRequest(result.ErrorMessage);
    }
}
```

### 2. Service 레이어에서 사용

```csharp
public interface IPlayerService
{
    Task<PlayerDto> GetPlayerAsync(string playerId);
    Task TransferGoldAsync(string fromPlayerId, string toPlayerId, long amount);
}

public class PlayerService : IPlayerService
{
    private readonly IGrainInvoker _invoker;
    private readonly ILogger<PlayerService> _logger;

    public PlayerService(
        IGrainInvoker invoker,
        ILogger<PlayerService> logger)
    {
        _invoker = invoker;
        _logger = logger;
    }

    public async Task<PlayerDto> GetPlayerAsync(string playerId)
    {
        var player = _invoker.GetGrain<IPlayerGrain>(playerId);
        var state = await player.GetDataAsync();

        return new PlayerDto
        {
            PlayerId = state.PlayerId,
            Name = state.Name,
            Level = state.Level,
            Gold = state.Gold
        };
    }

    public async Task TransferGoldAsync(string fromPlayerId, string toPlayerId, long amount)
    {
        var fromPlayer = _invoker.GetGrain<IPlayerGrain>(fromPlayerId);
        var toPlayer = _invoker.GetGrain<IPlayerGrain>(toPlayerId);

        // 트랜잭션 처리
        var hasEnough = await fromPlayer.HasEnoughGoldAsync(amount);
        if (!hasEnough)
            throw new InvalidOperationException("Insufficient gold");

        await fromPlayer.DeductGoldAsync(amount);
        await toPlayer.AddGoldAsync(amount);

        _logger.LogInformation(
            "Gold transfer: {From} -> {To}, Amount: {Amount}",
            fromPlayerId, toPlayerId, amount);
    }
}

// Program.cs에 등록
builder.Services.AddScoped<IPlayerService, PlayerService>();
```

### 3. 다양한 키 타입 사용

```csharp
public class GameService
{
    private readonly IGrainInvoker _invoker;

    public GameService(IGrainInvoker invoker)
    {
        _invoker = invoker;
    }

    // String 키
    public async Task ProcessPlayerAction(string playerId, string action)
    {
        var player = _invoker.GetGrain<IPlayerGrain>(playerId);
        await player.PerformActionAsync(action);
    }

    // Guid 키
    public async Task ProcessGameSession(Guid sessionId)
    {
        var session = _invoker.GetGrain<IGameSessionGrain>(sessionId);
        await session.StartAsync();
    }

    // Integer 키
    public async Task ProcessGameRoom(long roomId)
    {
        var room = _invoker.GetGrain<IGameRoomGrain>(roomId);
        await room.UpdateAsync();
    }

    // 복합 키 (Guid + String)
    public async Task ProcessShardedPlayer(Guid playerId, string region)
    {
        // playerId + region으로 샤딩된 Grain
        var player = _invoker.GetGrain<IShardedPlayerGrain>(playerId, region);
        await player.SyncDataAsync();
    }
}
```

### 4. 여러 Grain 조합

```csharp
public class PartyService
{
    private readonly IGrainInvoker _invoker;

    public PartyService(IGrainInvoker invoker)
    {
        _invoker = invoker;
    }

    public async Task<PartyInfoDto> CreatePartyAsync(string leaderId, List<string> memberIds)
    {
        var partyId = Guid.NewGuid();

        // 파티 Grain 생성
        var party = _invoker.GetGrain<IPartyGrain>(partyId);
        await party.CreateAsync(leaderId);

        // 리더 플레이어 Grain
        var leader = _invoker.GetGrain<IPlayerGrain>(leaderId);
        await leader.JoinPartyAsync(partyId);

        // 멤버 플레이어 Grain들
        foreach (var memberId in memberIds)
        {
            var member = _invoker.GetGrain<IPlayerGrain>(memberId);
            await member.JoinPartyAsync(partyId);
            await party.AddMemberAsync(memberId);
        }

        var partyData = await party.GetDataAsync();
        return new PartyInfoDto
        {
            PartyId = partyId,
            LeaderId = partyData.LeaderId,
            MemberIds = partyData.MemberIds
        };
    }
}
```

## 🔧 확장 패턴

### 1. 로깅 기능 추가

```csharp
// 로깅이 추가된 커스텀 구현
public class LoggingGrainInvoker : IGrainInvoker
{
    private readonly IClusterClient _client;
    private readonly ILogger<LoggingGrainInvoker> _logger;

    public LoggingGrainInvoker(
        IClusterClient client,
        ILogger<LoggingGrainInvoker> logger)
    {
        _client = client;
        _logger = logger;
    }

    public TGrain GetGrain<TGrain>(string key) where TGrain : IGrainWithStringKey
    {
        _logger.LogDebug("Getting grain {GrainType} with key {Key}",
            typeof(TGrain).Name, key);

        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Key cannot be null or empty", nameof(key));

        return _client.GetGrain<TGrain>(key);
    }

    // 다른 메서드도 동일하게 로깅 추가...
}

// 등록 시 커스텀 구현 사용
services.AddSingleton<IGrainInvoker, LoggingGrainInvoker>();
```

### 2. 캐싱 레이어 추가

```csharp
public class CachedGrainInvoker : IGrainInvoker
{
    private readonly IClusterClient _client;
    private readonly IMemoryCache _cache;

    public CachedGrainInvoker(
        IClusterClient client,
        IMemoryCache cache)
    {
        _client = client;
        _cache = cache;
    }

    public TGrain GetGrain<TGrain>(string key) where TGrain : IGrainWithStringKey
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Key cannot be null or empty", nameof(key));

        var cacheKey = $"{typeof(TGrain).Name}:{key}";

        return _cache.GetOrCreate(cacheKey, entry =>
        {
            entry.SlidingExpiration = TimeSpan.FromMinutes(5);
            return _client.GetGrain<TGrain>(key);
        });
    }

    // 다른 메서드도 동일하게 캐싱 추가...
}
```

### 3. 메트릭 수집

```csharp
public class MetricsGrainInvoker : IGrainInvoker
{
    private readonly IClusterClient _client;
    private readonly IMetrics _metrics;

    public MetricsGrainInvoker(
        IClusterClient client,
        IMetrics metrics)
    {
        _client = client;
        _metrics = metrics;
    }

    public TGrain GetGrain<TGrain>(string key) where TGrain : IGrainWithStringKey
    {
        _metrics.Increment("grain.invocations", new Dictionary<string, string>
        {
            ["grain_type"] = typeof(TGrain).Name,
            ["key_type"] = "string"
        });

        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Key cannot be null or empty", nameof(key));

        return _client.GetGrain<TGrain>(key);
    }

    // 다른 메서드도 동일하게 메트릭 수집...
}
```

## 🧪 테스트 작성

### 1. Mock을 사용한 단위 테스트

```csharp
using Moq;
using Xunit;

public class PlayerControllerTests
{
    [Fact]
    public async Task GetPlayer_ReturnsPlayerData()
    {
        // Arrange
        var mockInvoker = new Mock<IGrainInvoker>();
        var mockPlayerGrain = new Mock<IPlayerGrain>();

        mockPlayerGrain
            .Setup(x => x.GetDataAsync())
            .ReturnsAsync(new PlayerState
            {
                PlayerId = "player1",
                Name = "Alice",
                Level = 10
            });

        mockInvoker
            .Setup(x => x.GetGrain<IPlayerGrain>("player1"))
            .Returns(mockPlayerGrain.Object);

        var controller = new PlayerController(mockInvoker.Object, Mock.Of<ILogger<PlayerController>>());

        // Act
        var result = await controller.GetPlayer("player1");

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var playerData = Assert.IsType<PlayerState>(okResult.Value);
        Assert.Equal("Alice", playerData.Name);
        Assert.Equal(10, playerData.Level);
    }

    [Fact]
    public async Task BuyItem_Success_ReturnsOk()
    {
        // Arrange
        var mockInvoker = new Mock<IGrainInvoker>();
        var mockPlayerGrain = new Mock<IPlayerGrain>();

        mockPlayerGrain
            .Setup(x => x.BuyItemAsync("item1", 1))
            .ReturnsAsync(new BuyItemResult { Success = true });

        mockInvoker
            .Setup(x => x.GetGrain<IPlayerGrain>("player1"))
            .Returns(mockPlayerGrain.Object);

        var controller = new PlayerController(mockInvoker.Object, Mock.Of<ILogger<PlayerController>>());

        // Act
        var result = await controller.BuyItem("player1", new BuyItemRequest { ItemId = "item1", Quantity = 1 });

        // Assert
        Assert.IsType<OkObjectResult>(result);
    }
}
```

### 2. 테스트용 Fake 구현

```csharp
public class FakeGrainInvoker : IGrainInvoker
{
    private readonly Dictionary<string, object> _grains = new();

    public TGrain GetGrain<TGrain>(string key) where TGrain : IGrainWithStringKey
    {
        var grainKey = $"{typeof(TGrain).Name}:{key}";
        if (!_grains.ContainsKey(grainKey))
        {
            // 테스트용 Grain 인스턴스 생성
            _grains[grainKey] = CreateFakeGrain<TGrain>(key);
        }

        return (TGrain)_grains[grainKey];
    }

    private TGrain CreateFakeGrain<TGrain>(string key)
    {
        // 간단한 Fake Grain 생성 로직
        // 실제로는 Mock이나 TestGrain을 반환
        throw new NotImplementedException();
    }

    // 다른 메서드도 동일하게 구현...
}

// 테스트에서 사용
public class PlayerServiceTests
{
    [Fact]
    public async Task GetPlayerAsync_ReturnsDto()
    {
        var fakeInvoker = new FakeGrainInvoker();
        var service = new PlayerService(fakeInvoker, Mock.Of<ILogger<PlayerService>>());

        var result = await service.GetPlayerAsync("player1");

        Assert.NotNull(result);
    }
}
```

## 📊 디자인 패턴 분석

### 1. Facade(파사드) 패턴

```
복잡한 서브시스템 (Orleans)
├─ IClusterClient
├─ ClusterClient 구현
├─ GetGrain<T>(string)
├─ GetGrain<T>(Guid)
├─ GetGrain<T>(long)
└─ GetGrain<T>(Guid, string)

                 ↓ 단순화

       간단한 인터페이스
       IGrainInvoker
       ├─ GetGrain<T>(string)
       ├─ GetGrain<T>(Guid)
       ├─ GetGrain<T>(long)
       └─ GetGrain<T>(Guid, string)
```

**장점:**
- 복잡한 Orleans API를 단순한 인터페이스로 감춤
- 클라이언트 코드가 Orleans에 직접 의존하지 않음

### 2. Proxy(프록시) 패턴

```csharp
// GrainInvoker는 IClusterClient의 Proxy 역할
Client Code → IGrainInvoker → GrainInvoker → IClusterClient → Grain
                  ↑              ↑
              추상화          검증/로깅 추가 가능
```

**장점:**
- 실제 객체(IClusterClient) 접근을 제어
- 추가 기능(검증, 로깅) 삽입 가능

### 3. Dependency Injection(의존성 주입)

```csharp
// 인터페이스에 의존
public class PlayerController
{
    private readonly IGrainInvoker _invoker;  // ← 인터페이스

    public PlayerController(IGrainInvoker invoker)
    {
        _invoker = invoker;
    }
}

// 구현체는 DI 컨테이너가 주입
services.AddSingleton<IGrainInvoker, GrainInvoker>();
```

## ⚠️ 주의사항

### 1. Grain 참조는 경량 객체

```csharp
// ✅ 매번 GetGrain 호출해도 괜찮음 (경량 객체)
for (int i = 0; i < 1000; i++)
{
    var player = _invoker.GetGrain<IPlayerGrain>($"player{i}");
    await player.DoSomethingAsync();
}

// ❌ 캐싱 불필요 (오히려 메모리 낭비)
var cachedPlayers = new Dictionary<string, IPlayerGrain>();
for (int i = 0; i < 1000; i++)
{
    if (!cachedPlayers.ContainsKey($"player{i}"))
    {
        cachedPlayers[$"player{i}"] = _invoker.GetGrain<IPlayerGrain>($"player{i}");
    }
    await cachedPlayers[$"player{i}"].DoSomethingAsync();
}
```

**이유:**
- Grain 참조는 단순한 프록시 객체
- 실제 Grain 인스턴스는 Orleans 런타임이 관리
- GetGrain()은 참조만 생성하므로 비용이 거의 없음

### 2. 키 검증

```csharp
// ✅ GrainInvoker가 자동으로 검증
try
{
    var player = _invoker.GetGrain<IPlayerGrain>("");  // ArgumentException
}
catch (ArgumentException ex)
{
    _logger.LogError(ex, "Invalid key");
}

// ✅ Guid.Empty도 검증됨
try
{
    var session = _invoker.GetGrain<ISessionGrain>(Guid.Empty);  // ArgumentException
}
catch (ArgumentException ex)
{
    _logger.LogError(ex, "Invalid GUID key");
}
```

### 3. 제네릭 제약

```csharp
// ✅ 올바른 사용
_invoker.GetGrain<IPlayerGrain>("player1");  // IGrainWithStringKey

// ❌ 컴파일 에러: 제약 조건 위반
_invoker.GetGrain<IPlayerGrain>(123);  // IPlayerGrain은 IGrainWithStringKey이므로 long 불가

// ✅ 올바른 인터페이스 사용
public interface IPlayerGrain : IGrainWithStringKey { }  // String 키
public interface ISessionGrain : IGrainWithGuidKey { }   // Guid 키
public interface IRoomGrain : IGrainWithIntegerKey { }   // Long 키
```

## 🔗 관련 문서

- [재시도 정책 (RetryPolicy)](RetryPolicy.md) - GrainInvoker와 함께 사용되는 재시도 메커니즘
- [멱등성 (Idempotency)](Idempotency.md) - 중복 요청 방지
- [의존성 주입 (DI)](../Advanced/DependencyInjection.md) - IGrainInvoker 등록 및 주입
- [Orleans 기초](../Orleans-Basics.md) - Grain 키 타입 이해

## 📚 참고 자료

- [Orleans Grain References](https://learn.microsoft.com/dotnet/orleans/grains/grain-references)
- [Facade Pattern](https://refactoring.guru/design-patterns/facade)
- [Proxy Pattern](https://refactoring.guru/design-patterns/proxy)
