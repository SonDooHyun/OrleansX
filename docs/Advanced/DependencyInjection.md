# 의존성 주입 (Dependency Injection) 심층 가이드

## 📖 개요

의존성 주입(DI)은 객체가 필요로 하는 의존성을 외부에서 제공받는 디자인 패턴입니다. OrleansX는 .NET의 DI 컨테이너를 활용하여 모든 구성 요소를 관리합니다.

## 🎯 핵심 코드 분석

### OrleansX의 DI 등록

```csharp
// OrleansX.Client/Extensions/ServiceCollectionExtensions.cs (79-90번 라인)
public static IServiceCollection AddOrleansXClient(
    this IServiceCollection services,
    OrleansClientOptions options)
{
    // ... Orleans Client 설정 ...

    // GrainInvoker 등록
    services.AddSingleton<IGrainInvoker, GrainInvoker>();

    // Retry Policy 등록
    services.AddSingleton<IRetryPolicy>(
        new ExponentialRetryPolicy(...));

    // Idempotency Key Provider 등록
    services.AddSingleton<IIdempotencyKeyProvider, AsyncLocalIdempotencyKeyProvider>();

    return services;
}
```

## 🔍 AddSingleton 분해 설명

### 문법 구조

```csharp
services.AddSingleton<인터페이스, 구현체>();
                      ↓            ↓
services.AddSingleton<IGrainInvoker, GrainInvoker>();
```

**의미:**
- "누군가 `IGrainInvoker`를 요청하면, `GrainInvoker` 인스턴스를 제공하라"
- Singleton = 애플리케이션 전체에서 **단 하나의 인스턴스만** 생성하여 공유

### 실제 동작 흐름

```
1. 등록 단계 (Startup/Program.cs)
   services.AddSingleton<IGrainInvoker, GrainInvoker>();

   DI 컨테이너 내부:
   Dictionary<Type, ServiceDescriptor>
   {
       [IGrainInvoker] = {
           ServiceType = IGrainInvoker,
           ImplementationType = GrainInvoker,
           Lifetime = Singleton
       }
   }

2. 사용 단계 (Controller 생성 시)
   public PlayerController(IGrainInvoker invoker)

   DI 컨테이너가 처리:
   ① PlayerController를 생성하려고 함
   ② 생성자에 IGrainInvoker 필요
   ③ 컨테이너 조회: IGrainInvoker → GrainInvoker
   ④ GrainInvoker 생성 (이미 있으면 재사용)
   ⑤ PlayerController에 주입
```

## 💻 생명주기 비교

### 1. Singleton

```csharp
services.AddSingleton<IGrainInvoker, GrainInvoker>();

// 특징:
// - 애플리케이션 시작 시 1개 생성
// - 모든 요청이 동일한 인스턴스 공유
// - 애플리케이션 종료 시까지 유지

// 메모리 상태:
// [앱 시작] → GrainInvoker 인스턴스 #1 생성
// [요청 1]  → 인스턴스 #1 사용
// [요청 2]  → 인스턴스 #1 사용 (재사용)
// [요청 N]  → 인스턴스 #1 사용 (재사용)
// [앱 종료] → 인스턴스 #1 해제
```

**적합한 경우:**
- 상태가 없는 서비스 (Stateless)
- 공유해도 안전한 서비스
- 생성 비용이 큰 서비스

**OrleansX 사용 예:**
- `IGrainInvoker` - 상태 없음
- `IClusterClient` - 공유 필요
- `IRetryPolicy` - 불변 설정

### 2. Scoped

```csharp
services.AddScoped<IMyService, MyService>();

// 특징:
// - HTTP 요청마다 1개 생성
// - 같은 요청 내에서만 공유
// - 요청 종료 시 해제

// 메모리 상태:
// [요청 1] → MyService 인스턴스 #1 생성 → 사용 → 해제
// [요청 2] → MyService 인스턴스 #2 생성 → 사용 → 해제
// [요청 3] → MyService 인스턴스 #3 생성 → 사용 → 해제
```

**적합한 경우:**
- DbContext (데이터베이스 연결)
- HTTP 요청별 데이터 캐시
- 사용자 세션 정보

### 3. Transient

```csharp
services.AddTransient<IMyService, MyService>();

// 특징:
// - 요청할 때마다 항상 새로 생성
// - 공유되지 않음
// - 사용 후 바로 해제 가능

// 메모리 상태:
// [요청 1]
//   ├─ Controller 생성 → MyService 인스턴스 #1
//   └─ Service 생성   → MyService 인스턴스 #2 (새로 생성!)
```

**적합한 경우:**
- 경량 서비스
- 상태를 가진 Helper 클래스
- 매번 새로운 인스턴스가 필요한 경우

## 🏗️ 인터페이스 사용 이유

### 1. 테스트 용이성

```csharp
// 프로덕션 코드
public class PlayerController
{
    private readonly IGrainInvoker _invoker;

    public PlayerController(IGrainInvoker invoker)
    {
        _invoker = invoker; // 인터페이스에 의존
    }
}

// 테스트 코드
[Fact]
public async Task Test_GetPlayer()
{
    // Mock 객체 생성
    var mockInvoker = new Mock<IGrainInvoker>();
    mockInvoker.Setup(x => x.GetGrain<IPlayerGrain>("player1"))
               .Returns(/* 가짜 Grain */);

    // Mock을 주입하여 테스트
    var controller = new PlayerController(mockInvoker.Object);

    // ✅ 실제 Orleans 없이도 테스트 가능!
    var result = await controller.GetPlayerAsync("player1");
}
```

### 2. 구현 교체 용이

```csharp
// 원래 구현
public class GrainInvoker : IGrainInvoker
{
    // 기본 구현
}

// 캐싱 기능 추가
public class CachedGrainInvoker : IGrainInvoker
{
    private readonly IMemoryCache _cache;

    public TGrain GetGrain<TGrain>(string key)
    {
        // 캐시 확인 후 반환
    }
}

// 등록만 변경하면 전체 코드가 새 구현 사용!
// services.AddSingleton<IGrainInvoker, GrainInvoker>();
services.AddSingleton<IGrainInvoker, CachedGrainInvoker>(); // 교체
```

### 3. 의존성 역전 원칙 (DIP)

```
높은 수준 모듈 (Controller)
         ↓ 의존
    추상화 (IGrainInvoker)
         ↑ 구현
낮은 수준 모듈 (GrainInvoker)

Controller는 GrainInvoker의 구체적인 구현을 몰라도 됨!
```

## 🔄 생성자 주입 흐름

### 단계별 분석

```csharp
// 1. Controller 정의
public class GameController : ControllerBase
{
    private readonly IGrainInvoker _invoker;
    private readonly ILogger<GameController> _logger;

    // 생성자에 필요한 의존성 선언
    public GameController(
        IGrainInvoker invoker,
        ILogger<GameController> logger)
    {
        _invoker = invoker;
        _logger = logger;
    }
}

// 2. HTTP 요청 발생
// GET /api/game/player/123

// 3. ASP.NET Core가 GameController 생성 시도
//    ↓
// 4. DI 컨테이너에 의존성 요청:
//    "GameController 생성하려면 IGrainInvoker, ILogger<GameController> 필요해요"
//    ↓
// 5. 컨테이너가 의존성 해결:
//    IGrainInvoker → GrainInvoker 인스턴스 제공 (Singleton이므로 재사용)
//    ILogger<GameController> → LoggerFactory가 생성한 인스턴스 제공
//    ↓
// 6. GameController 생성:
//    new GameController(grainInvokerInstance, loggerInstance)
//    ↓
// 7. Action 메서드 실행
//    ↓
// 8. 응답 반환
```

### 의존성 체인 해결

```csharp
// GrainInvoker도 의존성을 가짐
public class GrainInvoker : IGrainInvoker
{
    private readonly IClusterClient _client;

    public GrainInvoker(IClusterClient client)
    {
        _client = client;
    }
}

// 전체 의존성 트리:
GameController
    ├─ IGrainInvoker (GrainInvoker)
    │   └─ IClusterClient (Orleans에서 등록)
    └─ ILogger<GameController> (ASP.NET Core에서 등록)

// DI 컨테이너가 자동으로 전체 트리를 해결!
```

## 🎮 OrleansX 실전 예제

### 1. Controller에서 사용

```csharp
[ApiController]
[Route("api/players")]
public class PlayerController : ControllerBase
{
    private readonly IGrainInvoker _invoker;
    private readonly IIdempotencyKeyProvider _keyProvider;
    private readonly ILogger<PlayerController> _logger;

    // ✅ 생성자 주입: DI 컨테이너가 자동으로 제공
    public PlayerController(
        IGrainInvoker invoker,
        IIdempotencyKeyProvider keyProvider,
        ILogger<PlayerController> logger)
    {
        _invoker = invoker;
        _keyProvider = keyProvider;
        _logger = logger;
    }

    [HttpPost("{id}/buy")]
    public async Task<IActionResult> BuyItem(string id, BuyItemRequest request)
    {
        var idempotencyKey = _keyProvider.GetIdempotencyKey();

        var player = _invoker.GetGrain<IPlayerGrain>(id);
        var result = await player.BuyItemAsync(request.ItemId, idempotencyKey!);

        _logger.LogInformation("Item purchased: {ItemId}", request.ItemId);

        return Ok(result);
    }
}
```

### 2. 커스텀 서비스 등록

```csharp
// IPlayerService.cs
public interface IPlayerService
{
    Task<PlayerData> GetPlayerDataAsync(string playerId);
}

// PlayerService.cs
public class PlayerService : IPlayerService
{
    private readonly IGrainInvoker _invoker;

    public PlayerService(IGrainInvoker invoker)
    {
        _invoker = invoker;
    }

    public async Task<PlayerData> GetPlayerDataAsync(string playerId)
    {
        var player = _invoker.GetGrain<IPlayerGrain>(playerId);
        return await player.GetDataAsync();
    }
}

// Program.cs에 등록
builder.Services.AddScoped<IPlayerService, PlayerService>();

// Controller에서 사용
public class GameController : ControllerBase
{
    private readonly IPlayerService _playerService;

    public GameController(IPlayerService playerService)
    {
        _playerService = playerService;
    }
}
```

## 🔧 고급 패턴

### 1. Factory 패턴

```csharp
public interface IPlayerGrainFactory
{
    IPlayerGrain Create(string playerId);
}

public class PlayerGrainFactory : IPlayerGrainFactory
{
    private readonly IGrainInvoker _invoker;

    public PlayerGrainFactory(IGrainInvoker invoker)
    {
        _invoker = invoker;
    }

    public IPlayerGrain Create(string playerId)
    {
        return _invoker.GetGrain<IPlayerGrain>(playerId);
    }
}

// 등록
services.AddSingleton<IPlayerGrainFactory, PlayerGrainFactory>();
```

### 2. Options 패턴

```csharp
// GameSettings.cs
public class GameSettings
{
    public int MaxPlayers { get; set; }
    public int ItemCacheTimeSeconds { get; set; }
}

// appsettings.json
{
  "GameSettings": {
    "MaxPlayers": 100,
    "ItemCacheTimeSeconds": 300
  }
}

// Program.cs
builder.Services.Configure<GameSettings>(
    builder.Configuration.GetSection("GameSettings"));

// Service에서 사용
public class GameService
{
    private readonly GameSettings _settings;

    public GameService(IOptions<GameSettings> options)
    {
        _settings = options.Value;
    }
}
```

### 3. 조건부 등록

```csharp
// 환경에 따라 다른 구현 사용
if (builder.Environment.IsDevelopment())
{
    services.AddSingleton<IPaymentService, MockPaymentService>();
}
else
{
    services.AddSingleton<IPaymentService, RealPaymentService>();
}
```

## 🐛 일반적인 실수

### 1. Captive Dependency

```csharp
// ❌ Bad: Singleton이 Scoped를 참조
services.AddSingleton<MySingletonService>(); // Singleton
services.AddScoped<MyDbContext>(); // Scoped

public class MySingletonService
{
    // ❌ Singleton이 Scoped를 참조하면 문제!
    // Scoped는 요청마다 생성되어야 하는데 Singleton에 캡처됨
    public MySingletonService(MyDbContext db) { }
}

// ✅ Good: Scoped가 Singleton을 참조 (OK)
public class MyScopedService
{
    public MyScopedService(MySingletonService singleton) { } // OK
}
```

### 2. 순환 의존성

```csharp
// ❌ Bad: A → B → A
public class ServiceA
{
    public ServiceA(ServiceB b) { }
}

public class ServiceB
{
    public ServiceB(ServiceA a) { } // 순환!
}

// ✅ Good: 인터페이스로 분리
public interface IServiceADependency { }
public class ServiceA : IServiceADependency { }

public class ServiceB
{
    public ServiceB(IServiceADependency a) { }
}
```

## 📊 성능 고려사항

| 생명주기 | 생성 오버헤드 | 메모리 사용 | 스레드 안전성 |
|---------|-------------|------------|-------------|
| Singleton | 1회만 | 낮음 | 필요 |
| Scoped | 요청당 1회 | 중간 | 불필요 |
| Transient | 호출마다 | 높음 | 불필요 |

## 🔗 관련 문서

- [AsyncLocal](AsyncLocal.md) - DI와 함께 사용되는 컨텍스트 저장
- [멱등성](../Client/Idempotency.md) - IIdempotencyKeyProvider 사용 예제
- [GrainInvoker](../Client/GrainInvoker.md) - DI로 주입되는 주요 컴포넌트
