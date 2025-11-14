# 멱등성 (Idempotency)

## 📖 개요

**멱등성(Idempotency)**은 같은 요청을 여러 번 실행해도 결과가 동일하게 유지되는 속성입니다. OrleansX는 `IIdempotencyKeyProvider`와 `AsyncLocalIdempotencyKeyProvider`를 제공하여 중복 요청 방지를 지원합니다.

## 🎯 왜 필요한가?

### 문제 상황

```csharp
// 네트워크 불안정으로 동일한 요청이 3번 전송됨
POST /api/payment { userId: "player1", amount: 1000 }  // 1차 시도
POST /api/payment { userId: "player1", amount: 1000 }  // 재시도
POST /api/payment { userId: "player1", amount: 1000 }  // 재시도

// ❌ 멱등성 없이 구현하면
// 결과: 3000원 결제됨! 💸💸💸 (중복 결제 발생)

// ✅ 멱등성 적용하면
// 결과: 1000원만 결제됨 (첫 번째 요청만 처리)
```

### 실제 사례

| 시나리오 | 문제 | 해결 |
|---------|------|------|
| **모바일 게임** | 네트워크 끊김으로 아이템 구매 요청 중복 전송 | 같은 `idempotencyKey`로 첫 번째만 처리 |
| **결제 시스템** | 사용자가 결제 버튼을 여러 번 클릭 | 결제 ID로 중복 결제 방지 |
| **인벤토리** | 서버 타임아웃으로 아이템 지급 API 재시도 | 보상 ID로 중복 지급 방지 |
| **거래 시스템** | 플레이어 간 거래 확정 요청이 중복 전송 | 거래 ID로 중복 처리 방지 |

## 🏗️ 구현 구조

### 1. IIdempotencyKeyProvider 인터페이스

```csharp
// OrleansX.Abstractions/IIdempotencyKeyProvider.cs
namespace OrleansX.Abstractions;

public interface IIdempotencyKeyProvider
{
    /// <summary>
    /// 현재 컨텍스트의 Idempotency Key를 가져옵니다.
    /// </summary>
    string? GetIdempotencyKey();

    /// <summary>
    /// Idempotency Key를 설정합니다.
    /// </summary>
    void SetIdempotencyKey(string key);
}
```

### 2. AsyncLocalIdempotencyKeyProvider 구현

```csharp
// OrleansX.Client/Idempotency/AsyncLocalIdempotencyKeyProvider.cs
namespace OrleansX.Client.Idempotency;

public class AsyncLocalIdempotencyKeyProvider : IIdempotencyKeyProvider
{
    // AsyncLocal: 비동기 컨텍스트별로 독립적인 저장소
    private static readonly AsyncLocal<string?> _idempotencyKey = new();

    public string? GetIdempotencyKey()
    {
        return _idempotencyKey.Value;
    }

    public void SetIdempotencyKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Idempotency key cannot be null or empty");

        _idempotencyKey.Value = key;
    }
}
```

## 🔍 AsyncLocal 이해하기

### AsyncLocal이란?

`AsyncLocal<T>`는 .NET의 특별한 저장소로, **비동기 흐름 전체에서 값을 공유**하면서도 **다른 요청과는 격리**됩니다.

### 일반 변수 vs AsyncLocal

```csharp
// ❌ 문제: 일반 정적 변수 사용
public class BadIdempotencyProvider
{
    private static string? _key; // 모든 요청이 공유!

    public void SetKey(string key)
    {
        _key = key;
    }

    public string? GetKey()
    {
        return _key; // 다른 요청의 키를 가져올 수 있음!
    }
}

// 시나리오:
// 요청 1: SetKey("key-001")
// 요청 2: SetKey("key-002") ← 이 순간 _key가 "key-002"로 변경
// 요청 1: GetKey() → "key-002" 반환 (잘못됨! 💥)
```

```csharp
// ✅ 해결: AsyncLocal 사용
public class GoodIdempotencyProvider
{
    private static readonly AsyncLocal<string?> _key = new();

    public void SetKey(string key)
    {
        _key.Value = key; // 현재 비동기 컨텍스트에만 저장
    }

    public string? GetKey()
    {
        return _key.Value; // 현재 비동기 컨텍스트의 값만 반환
    }
}

// 시나리오:
// 요청 1 컨텍스트: SetKey("key-001")
// 요청 2 컨텍스트: SetKey("key-002")
// 요청 1 컨텍스트: GetKey() → "key-001" 반환 (정확!) ✅
// 요청 2 컨텍스트: GetKey() → "key-002" 반환 (정확!) ✅
```

### AsyncLocal의 작동 방식

```
HTTP 요청 1 (비동기 컨텍스트 A)
│
├─ Middleware: SetIdempotencyKey("req-001")
│   └─ AsyncLocal에 저장: [Context A] → "req-001"
│
├─ Controller (async)
│   └─ GetIdempotencyKey() → "req-001" ✅
│
├─ Service (async)
│   └─ GetIdempotencyKey() → "req-001" ✅
│
└─ Grain 호출 (async)
    └─ GetIdempotencyKey() → "req-001" ✅


HTTP 요청 2 (비동기 컨텍스트 B) - 동시 실행
│
├─ Middleware: SetIdempotencyKey("req-002")
│   └─ AsyncLocal에 저장: [Context B] → "req-002"
│
├─ Controller (async)
│   └─ GetIdempotencyKey() → "req-002" ✅
│
└─ Service (async)
    └─ GetIdempotencyKey() → "req-002" ✅
```

## 💻 사용 방법

### 1. Middleware 작성

```csharp
// IdempotencyMiddleware.cs
public class IdempotencyMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IIdempotencyKeyProvider _keyProvider;

    public IdempotencyMiddleware(
        RequestDelegate next,
        IIdempotencyKeyProvider keyProvider)
    {
        _next = next;
        _keyProvider = keyProvider;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // HTTP 헤더에서 Idempotency Key 추출
        if (context.Request.Headers.TryGetValue("X-Idempotency-Key", out var key))
        {
            // AsyncLocal에 저장 (이 요청의 모든 async 호출에서 접근 가능)
            _keyProvider.SetIdempotencyKey(key.ToString());
        }
        else if (context.Request.Method is "POST" or "PUT" or "PATCH")
        {
            // POST/PUT/PATCH는 Idempotency Key 필수
            context.Response.StatusCode = 400;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "X-Idempotency-Key header is required"
            });
            return;
        }

        await _next(context);
    }
}

// Program.cs에 등록
app.UseMiddleware<IdempotencyMiddleware>();
```

### 2. Controller에서 사용

```csharp
// PaymentController.cs
public class PaymentController : ControllerBase
{
    private readonly IGrainInvoker _invoker;
    private readonly IIdempotencyKeyProvider _keyProvider;

    public PaymentController(
        IGrainInvoker invoker,
        IIdempotencyKeyProvider keyProvider)
    {
        _invoker = invoker;
        _keyProvider = keyProvider;
    }

    [HttpPost("process")]
    public async Task<IActionResult> ProcessPayment(PaymentRequest request)
    {
        // Middleware에서 설정한 키 가져오기
        var idempotencyKey = _keyProvider.GetIdempotencyKey();

        if (idempotencyKey == null)
        {
            return BadRequest("Idempotency key is required");
        }

        var paymentGrain = _invoker.GetGrain<IPaymentGrain>(request.UserId);

        // Grain에 idempotencyKey 전달
        var result = await paymentGrain.ProcessPaymentAsync(
            request.Amount,
            idempotencyKey);

        return Ok(result);
    }
}
```

### 3. Grain에서 중복 처리 방지

```csharp
// PaymentGrain.cs
[GenerateSerializer]
public class PaymentState
{
    [Id(0)] public int Balance { get; set; }
    [Id(1)] public HashSet<string> ProcessedKeys { get; set; } = new();
}

public class PaymentGrain : StatefulGrainBase<PaymentState>, IPaymentGrain
{
    public PaymentGrain(
        [PersistentState("payment")] IPersistentState<PaymentState> state,
        ILogger<PaymentGrain> logger)
        : base(state, logger)
    {
    }

    public async Task<PaymentResult> ProcessPaymentAsync(int amount, string idempotencyKey)
    {
        // 1. 이미 처리한 요청인지 확인
        if (State.ProcessedKeys.Contains(idempotencyKey))
        {
            Logger.LogWarning(
                "Duplicate payment request detected: {Key}",
                idempotencyKey);

            return new PaymentResult
            {
                Success = true,
                Message = "Already processed",
                AlreadyProcessed = true
            };
        }

        // 2. 실제 결제 처리
        await UpdateStateAsync(state =>
        {
            state.Balance -= amount;
            state.ProcessedKeys.Add(idempotencyKey);
        });

        Logger.LogInformation(
            "Payment processed: {Amount}, Key: {Key}",
            amount, idempotencyKey);

        return new PaymentResult
        {
            Success = true,
            Message = "Payment processed",
            NewBalance = State.Balance
        };
    }
}
```

## 🎮 게임 서버 예제

### 아이템 구매 중복 방지

```csharp
// ShopController.cs
[ApiController]
[Route("api/shop")]
public class ShopController : ControllerBase
{
    private readonly IGrainInvoker _invoker;
    private readonly IIdempotencyKeyProvider _keyProvider;

    public ShopController(
        IGrainInvoker invoker,
        IIdempotencyKeyProvider keyProvider)
    {
        _invoker = invoker;
        _keyProvider = keyProvider;
    }

    [HttpPost("buy")]
    public async Task<IActionResult> BuyItem(BuyItemRequest request)
    {
        var idempotencyKey = _keyProvider.GetIdempotencyKey();

        var player = _invoker.GetGrain<IPlayerGrain>(request.PlayerId);
        var result = await player.BuyItemAsync(
            request.ItemId,
            request.Price,
            idempotencyKey!);

        return Ok(result);
    }
}

// PlayerGrain.cs
[GenerateSerializer]
public class PlayerState
{
    [Id(0)] public int Gold { get; set; } = 10000;
    [Id(1)] public List<string> Items { get; set; } = new();
    [Id(2)] public HashSet<string> ProcessedPurchases { get; set; } = new();
}

public class PlayerGrain : StatefulGrainBase<PlayerState>, IPlayerGrain
{
    public async Task<BuyResult> BuyItemAsync(
        string itemId,
        int price,
        string idempotencyKey)
    {
        // 중복 구매 체크
        if (State.ProcessedPurchases.Contains(idempotencyKey))
        {
            Logger.LogWarning(
                "Duplicate purchase detected: Player={PlayerId}, Item={ItemId}, Key={Key}",
                this.GetPrimaryKeyString(), itemId, idempotencyKey);

            return new BuyResult
            {
                Success = true,
                Message = "Already purchased",
                IsDuplicate = true
            };
        }

        // 골드 체크
        if (State.Gold < price)
        {
            return new BuyResult
            {
                Success = false,
                Message = "Insufficient gold"
            };
        }

        // 구매 처리
        await UpdateStateAsync(state =>
        {
            state.Gold -= price;
            state.Items.Add(itemId);
            state.ProcessedPurchases.Add(idempotencyKey);
        });

        Logger.LogInformation(
            "Item purchased: Player={PlayerId}, Item={ItemId}, Price={Price}, Key={Key}",
            this.GetPrimaryKeyString(), itemId, price, idempotencyKey);

        return new BuyResult
        {
            Success = true,
            Message = "Item purchased successfully",
            RemainingGold = State.Gold
        };
    }
}

// 클라이언트 요청 예제
// 네트워크 불안정으로 3번 전송되어도 1번만 처리됨!
POST https://api.game.com/api/shop/buy
Headers:
  X-Idempotency-Key: buy-sword-12345-67890
Body:
  {
    "playerId": "player-001",
    "itemId": "sword-legendary",
    "price": 1000
  }

// 응답 (첫 번째):
{
  "success": true,
  "message": "Item purchased successfully",
  "remainingGold": 9000
}

// 응답 (두 번째, 세 번째 - 중복):
{
  "success": true,
  "message": "Already purchased",
  "isDuplicate": true
}
```

## 📊 전체 흐름도

```
1. 클라이언트
   │
   └─> POST /api/shop/buy
       Headers: X-Idempotency-Key: buy-item-12345

2. IdempotencyMiddleware
   │
   ├─> 헤더에서 "buy-item-12345" 추출
   └─> _keyProvider.SetIdempotencyKey("buy-item-12345")
       └─> AsyncLocal[현재 컨텍스트] = "buy-item-12345"

3. ShopController
   │
   ├─> _keyProvider.GetIdempotencyKey()
   │   └─> AsyncLocal[현재 컨텍스트] → "buy-item-12345"
   │
   └─> player.BuyItemAsync(itemId, price, "buy-item-12345")

4. PlayerGrain
   │
   ├─> State.ProcessedPurchases에 "buy-item-12345" 있나?
   │   ├─> Yes: "Already processed" 반환 (중복!)
   │   └─> No: 구매 처리 + Key 저장
   │
   └─> 응답 반환

5. 같은 요청이 다시 오면
   │
   └─> 4번의 Yes 분기로 이동 → 중복 처리 방지 ✅
```

## 🔒 보안 고려사항

### 1. Idempotency Key 생성

```csharp
// ✅ Good: UUID v4 사용
var key = Guid.NewGuid().ToString(); // "a1b2c3d4-..."

// ✅ Good: 타임스탬프 + 랜덤
var key = $"{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}-{Guid.NewGuid():N}";

// ❌ Bad: 예측 가능한 키
var key = $"{userId}-{DateTime.Now.Ticks}"; // 공격자가 추측 가능
```

### 2. 키 저장소 관리

```csharp
// ⚠️ 주의: 무한정 저장하면 메모리 부족
// 해결책 1: 만료 시간 설정
[GenerateSerializer]
public class ProcessedKey
{
    [Id(0)] public string Key { get; set; } = string.Empty;
    [Id(1)] public DateTime ProcessedAt { get; set; }
}

// 24시간 지난 키는 제거
await UpdateStateAsync(state =>
{
    var expiredKeys = state.ProcessedKeys
        .Where(k => k.ProcessedAt < DateTime.UtcNow.AddHours(-24))
        .Select(k => k.Key)
        .ToList();

    foreach (var key in expiredKeys)
    {
        state.ProcessedKeys.Remove(key);
    }
});

// 해결책 2: Redis나 별도 저장소 사용 (TTL 지원)
```

## 📈 성능 최적화

### 1. HashSet 사용

```csharp
// ✅ Good: HashSet (O(1) 조회)
public HashSet<string> ProcessedKeys { get; set; } = new();

if (State.ProcessedKeys.Contains(key)) // 빠름!

// ❌ Bad: List (O(n) 조회)
public List<string> ProcessedKeys { get; set; } = new();

if (State.ProcessedKeys.Contains(key)) // 느림!
```

### 2. Bloom Filter 고려

```csharp
// 대량의 키를 다루는 경우
// Bloom Filter: 메모리 효율적, 빠른 조회
// 단, False Positive 가능 (있다고 판단했는데 실제로는 없을 수 있음)
```

## ⚠️ 주의사항

### 1. 분산 환경에서의 Race Condition

```csharp
// 문제: 거의 동시에 같은 키로 2개 요청이 들어오면?
// Grain은 단일 스레드이므로 자동으로 직렬화됨!
// Orleans가 보장하는 턴 기반 동시성 덕분에 안전함 ✅
```

### 2. Stateless Grain에서는 적합하지 않음

```csharp
// ❌ Bad: Stateless Grain
// Activation마다 독립적이므로 ProcessedKeys가 공유되지 않음
[StatelessWorker]
public class StatelessPaymentGrain : StatelessGrainBase
{
    // 이 HashSet은 각 인스턴스마다 별개!
    private HashSet<string> _processedKeys = new();
}

// ✅ Good: Stateful Grain 또는 외부 저장소
public class StatefulPaymentGrain : StatefulGrainBase<PaymentState>
{
    // State는 영속적으로 저장됨
}
```

## 📚 참고 자료

- [Stripe API Idempotency](https://stripe.com/docs/api/idempotent_requests)
- [RFC 7231 - Idempotent Methods](https://tools.ietf.org/html/rfc7231#section-4.2.2)
- [AsyncLocal<T> 공식 문서](https://learn.microsoft.com/dotnet/api/system.threading.asynclocal-1)

## 🔗 관련 문서

- [AsyncLocal 상세 가이드](../Advanced/AsyncLocal.md)
- [의존성 주입](../Advanced/DependencyInjection.md)
- [Retry Policy](RetryPolicy.md)
