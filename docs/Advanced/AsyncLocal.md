# AsyncLocal<T> 심층 가이드

## 📖 개요

`AsyncLocal<T>`는 .NET의 특별한 저장소로, **비동기 흐름(async flow) 전체에서 데이터를 공유**하면서도 **다른 비동기 컨텍스트와는 완전히 격리**됩니다.

## 🎯 핵심 개념

### AsyncLocal vs 일반 변수 vs ThreadLocal

| 특징 | 일반 정적 변수 | ThreadLocal<T> | AsyncLocal<T> |
|------|---------------|----------------|---------------|
| **스레드 간 격리** | ❌ 모든 스레드가 공유 | ✅ 스레드별로 독립 | ✅ 비동기 컨텍스트별로 독립 |
| **async/await 추적** | ❌ 추적 안 됨 | ❌ 추적 안 됨 | ✅ 추적됨 |
| **사용 사례** | 전역 설정 | 스레드 로컬 캐시 | HTTP 요청 추적, 트랜잭션 ID |

### 문제 상황 예제

```csharp
// ❌ 일반 정적 변수의 문제
public class BadRequestIdProvider
{
    private static string? _requestId; // 모든 요청이 공유!

    public static void SetRequestId(string id)
    {
        _requestId = id;
    }

    public static string? GetRequestId()
    {
        return _requestId;
    }
}

// 시나리오:
// [시간 0ms] 요청 A: SetRequestId("req-A")
// [시간 1ms] 요청 B: SetRequestId("req-B") ← _requestId가 "req-B"로 변경됨!
// [시간 2ms] 요청 A: GetRequestId() → "req-B" 반환 (잘못됨! 💥)
// [시간 3ms] 요청 B: GetRequestId() → "req-B" 반환 (정확)
```

```csharp
// ❌ ThreadLocal의 문제
public class AlsoBadRequestIdProvider
{
    private static readonly ThreadLocal<string?> _requestId = new();

    public static void SetRequestId(string id)
    {
        _requestId.Value = id;
    }

    public static string? GetRequestId()
    {
        return _requestId.Value;
    }
}

// 문제: async/await에서 스레드가 바뀔 수 있음!
// [Thread 1] SetRequestId("req-A")
// [Thread 1] await SomeAsyncCall()
// [Thread 2] GetRequestId() → null! (다른 스레드로 전환됨 💥)
```

```csharp
// ✅ AsyncLocal의 해결
public class GoodRequestIdProvider
{
    private static readonly AsyncLocal<string?> _requestId = new();

    public static void SetRequestId(string id)
    {
        _requestId.Value = id;
    }

    public static string? GetRequestId()
    {
        return _requestId.Value;
    }
}

// ✅ 정상 동작:
// [요청 A 컨텍스트] SetRequestId("req-A")
// [요청 B 컨텍스트] SetRequestId("req-B")
// [요청 A 컨텍스트] await SomeAsync()
//   └─> 스레드가 바뀌어도 "req-A" 유지!
// [요청 A 컨텍스트] GetRequestId() → "req-A" ✅
// [요청 B 컨텍스트] GetRequestId() → "req-B" ✅
```

## 🔍 작동 원리

### 비동기 컨텍스트 추적

```csharp
HTTP 요청 A 시작
│
├─ SetRequestId("req-A")
│   └─ AsyncLocal[컨텍스트 A] = "req-A"
│
├─ async Method1()
│   │  [Thread 5에서 실행]
│   └─ GetRequestId() → "req-A" ✅
│
├─ await DatabaseCall()
│   │  [Thread 8로 전환]
│   └─ GetRequestId() → "req-A" ✅ (스레드 바뀌어도 유지!)
│
├─ async Method2()
│   │  [Thread 3으로 전환]
│   └─ GetRequestId() → "req-A" ✅ (여전히 유지!)
│
└─ 응답 반환


HTTP 요청 B 시작 (동시에 실행 중)
│
├─ SetRequestId("req-B")
│   └─ AsyncLocal[컨텍스트 B] = "req-B"
│
├─ async Method1()
│   │  [Thread 5에서 실행] ← 요청 A와 같은 스레드!
│   └─ GetRequestId() → "req-B" ✅ (요청 A와 섞이지 않음!)
│
└─ await DatabaseCall()
    └─ GetRequestId() → "req-B" ✅
```

### 내부 메커니즘

```csharp
// .NET 런타임이 내부적으로 관리하는 것 (개념)
class ExecutionContext
{
    // 각 비동기 컨텍스트마다 독립적인 Dictionary
    Dictionary<AsyncLocal<T>, T> _asyncLocalValues;
}

// await 할 때마다:
// 1. 현재 ExecutionContext를 캡처
// 2. 새 스레드에서 작업 재개 시 ExecutionContext 복원
// 3. AsyncLocal 값들이 그대로 유지됨!
```

## 💻 OrleansX에서의 사용

### IIdempotencyKeyProvider 구현

```csharp
// OrleansX.Client/Idempotency/AsyncLocalIdempotencyKeyProvider.cs
public class AsyncLocalIdempotencyKeyProvider : IIdempotencyKeyProvider
{
    // ✅ AsyncLocal 사용: 각 HTTP 요청마다 독립적인 키
    private static readonly AsyncLocal<string?> _idempotencyKey = new();

    public string? GetIdempotencyKey()
    {
        return _idempotencyKey.Value;
    }

    public void SetIdempotencyKey(string key)
    {
        _idempotencyKey.Value = key;
    }
}

// 사용 흐름:
// 1. Middleware에서 SetIdempotencyKey("key-123")
// 2. Controller/Service/Grain 어디서든 GetIdempotencyKey() 호출
// 3. 같은 HTTP 요청 컨텍스트 내에서는 항상 "key-123" 반환
// 4. 다른 HTTP 요청에서는 완전히 다른 값
```

## 🎮 실전 예제

### 1. HTTP 요청 추적

```csharp
// RequestTracingMiddleware.cs
public class RequestTracingMiddleware
{
    private static readonly AsyncLocal<RequestContext> _context = new();
    private readonly RequestDelegate _next;

    public async Task InvokeAsync(HttpContext httpContext)
    {
        // 요청 컨텍스트 생성
        var requestContext = new RequestContext
        {
            RequestId = Guid.NewGuid().ToString(),
            UserId = httpContext.User.FindFirst("sub")?.Value,
            StartTime = DateTime.UtcNow
        };

        // AsyncLocal에 저장
        _context.Value = requestContext;

        try
        {
            await _next(httpContext);
        }
        finally
        {
            // 로깅: 요청 처리 시간
            var duration = DateTime.UtcNow - requestContext.StartTime;
            Console.WriteLine(
                $"Request {requestContext.RequestId} completed in {duration.TotalMilliseconds}ms");
        }
    }

    public static RequestContext? Current => _context.Value;
}

// Controller에서 사용
public class GameController : ControllerBase
{
    [HttpPost("battle")]
    public async Task<IActionResult> StartBattle()
    {
        // 어디서든 현재 요청 컨텍스트에 접근 가능!
        var context = RequestTracingMiddleware.Current;
        _logger.LogInformation(
            "Starting battle for request {RequestId}, user {UserId}",
            context?.RequestId, context?.UserId);

        await _battleService.StartAsync();
        return Ok();
    }
}

// Service에서도 접근 가능
public class BattleService
{
    public async Task StartAsync()
    {
        var context = RequestTracingMiddleware.Current;
        _logger.LogInformation(
            "BattleService.StartAsync called from request {RequestId}",
            context?.RequestId);

        // async 메서드를 여러 번 거쳐도 계속 유지됨!
        await Task.Delay(100);

        var context2 = RequestTracingMiddleware.Current;
        // context == context2 (동일한 인스턴스!)
    }
}
```

### 2. 트랜잭션 컨텍스트

```csharp
// TransactionScope를 AsyncLocal로 구현하는 개념
public class AsyncTransactionScope : IDisposable
{
    private static readonly AsyncLocal<AsyncTransactionScope?> _current = new();

    public string TransactionId { get; }
    private bool _committed;

    public AsyncTransactionScope()
    {
        TransactionId = Guid.NewGuid().ToString();
        _current.Value = this;
    }

    public static AsyncTransactionScope? Current => _current.Value;

    public void Complete()
    {
        _committed = true;
    }

    public void Dispose()
    {
        if (!_committed)
        {
            // 롤백
            Console.WriteLine($"Transaction {TransactionId} rolled back");
        }
        else
        {
            // 커밋
            Console.WriteLine($"Transaction {TransactionId} committed");
        }

        _current.Value = null;
    }
}

// 사용 예
public async Task ProcessOrderAsync(Order order)
{
    using (var transaction = new AsyncTransactionScope())
    {
        // 여러 async 호출에서 같은 트랜잭션 사용
        await _inventoryService.ReserveItemAsync(order.ItemId);
        await _paymentService.ChargeAsync(order.Amount);
        await _shippingService.CreateShipmentAsync(order);

        transaction.Complete();
    }
}

// Service 내부에서
public class InventoryService
{
    public async Task ReserveItemAsync(string itemId)
    {
        var tx = AsyncTransactionScope.Current;
        Console.WriteLine($"Reserving item in transaction {tx?.TransactionId}");

        // 같은 트랜잭션 컨텍스트에서 동작!
    }
}
```

### 3. 로깅 컨텍스트

```csharp
// LogContext.cs
public static class LogContext
{
    private static readonly AsyncLocal<Dictionary<string, object>> _properties = new();

    public static IDisposable Push(string key, object value)
    {
        var dict = _properties.Value ?? new Dictionary<string, object>();
        _properties.Value = new Dictionary<string, object>(dict)
        {
            [key] = value
        };

        return new PopContext(() =>
        {
            _properties.Value = dict;
        });
    }

    public static Dictionary<string, object> GetAll()
    {
        return _properties.Value ?? new Dictionary<string, object>();
    }

    private class PopContext : IDisposable
    {
        private readonly Action _onDispose;

        public PopContext(Action onDispose)
        {
            _onDispose = onDispose;
        }

        public void Dispose() => _onDispose();
    }
}

// 사용 예
public async Task ProcessUserActionAsync(string userId, string action)
{
    using (LogContext.Push("UserId", userId))
    using (LogContext.Push("Action", action))
    {
        _logger.LogInformation("Processing action"); // 자동으로 UserId, Action 포함

        await Step1Async();
        await Step2Async();
        // 모든 단계에서 UserId, Action이 로그에 포함됨!
    }
}
```

## ⚠️ 주의사항

### 1. 메모리 누수 방지

```csharp
// ❌ Bad: 큰 객체를 AsyncLocal에 저장하고 해제하지 않음
private static readonly AsyncLocal<byte[]> _data = new();

public void ProcessRequest()
{
    _data.Value = new byte[1024 * 1024]; // 1MB
    // ... 처리
    // ❌ 해제하지 않으면 GC될 때까지 메모리 점유!
}

// ✅ Good: 명시적으로 해제
public void ProcessRequest()
{
    try
    {
        _data.Value = new byte[1024 * 1024];
        // ... 처리
    }
    finally
    {
        _data.Value = null; // 명시적 해제
    }
}
```

### 2. 값 타입 vs 참조 타입

```csharp
// AsyncLocal은 값 자체를 저장, 복사하지 않음
private static readonly AsyncLocal<MyClass> _obj = new();

_obj.Value = new MyClass { Name = "Test" };

// ❌ 다른 곳에서 변경하면 모든 곳에 영향
_obj.Value.Name = "Changed"; // 참조를 통해 변경됨!

// ✅ 불변 객체 사용 권장
private static readonly AsyncLocal<string> _id = new(); // string은 불변
```

### 3. 부모-자식 Task 관계

```csharp
public async Task ParentTask()
{
    _asyncLocal.Value = "parent";

    // 자식 Task는 부모의 AsyncLocal 값을 상속받음
    await Task.Run(() =>
    {
        var value = _asyncLocal.Value; // "parent" 상속됨

        _asyncLocal.Value = "child"; // 자식에서만 변경
    });

    // 부모는 영향 받지 않음
    var parentValue = _asyncLocal.Value; // 여전히 "parent"
}
```

## 🔧 디버깅 팁

### 1. 값이 null인 경우

```csharp
public static class AsyncLocalDebugger
{
    private static readonly AsyncLocal<string?> _value = new();

    public static void Set(string value, [CallerMemberName] string caller = "")
    {
        Console.WriteLine($"[{Thread.CurrentThread.ManagedThreadId}] Set called from {caller}: {value}");
        _value.Value = value;
    }

    public static string? Get([CallerMemberName] string caller = "")
    {
        var result = _value.Value;
        Console.WriteLine($"[{Thread.CurrentThread.ManagedThreadId}] Get called from {caller}: {result ?? "null"}");
        return result;
    }
}

// 사용하면 어디서 설정/조회되는지 추적 가능
```

### 2. ExecutionContext가 전파되지 않는 경우

```csharp
// ❌ ExecutionContext가 전파되지 않는 케이스들

// 1. ConfigureAwait(false) 사용 시
await SomeAsync().ConfigureAwait(false);
// → 이후 코드에서 AsyncLocal 값이 손실될 수 있음!

// 2. Thread.Start로 새 스레드 생성
new Thread(() =>
{
    // AsyncLocal 값이 전파되지 않음!
    var value = _asyncLocal.Value; // null
}).Start();

// 3. ThreadPool.QueueUserWorkItem
ThreadPool.QueueUserWorkItem(_ =>
{
    // AsyncLocal 값이 전파되지 않음!
});

// ✅ Task.Run은 ExecutionContext를 전파함
await Task.Run(() =>
{
    var value = _asyncLocal.Value; // 전파됨! ✅
});
```

## 📊 성능 고려사항

```csharp
// AsyncLocal은 빠르지만, 과도한 사용은 피하기

// ✅ Good: 요청당 몇 개 정도
private static readonly AsyncLocal<string?> _requestId = new();
private static readonly AsyncLocal<string?> _userId = new();
private static readonly AsyncLocal<string?> _correlationId = new();

// ⚠️ 주의: 너무 많은 AsyncLocal
// 각 AsyncLocal마다 ExecutionContext에 오버헤드 발생
private static readonly AsyncLocal<object> _value1 = new();
private static readonly AsyncLocal<object> _value2 = new();
// ... 수십 개의 AsyncLocal
```

## 📚 참고 자료

- [AsyncLocal<T> 공식 문서](https://learn.microsoft.com/dotnet/api/system.threading.asynclocal-1)
- [ExecutionContext](https://learn.microsoft.com/dotnet/api/system.threading.executioncontext)
- [Understanding ExecutionContext](https://devblogs.microsoft.com/dotnet/understanding-the-whys-whats-and-whens-of-valuetask/)

## 🔗 관련 문서

- [멱등성 (Idempotency)](../Client/Idempotency.md) - AsyncLocal 실전 활용 예제
- [의존성 주입](DependencyInjection.md)
- [Silo 구성](../Hosting/SiloConfiguration.md)
