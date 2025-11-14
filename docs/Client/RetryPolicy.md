# 재시도 정책 (Retry Policy) 심층 가이드

## 📖 개요

`RetryPolicy`는 분산 시스템에서 발생하는 **일시적인 오류를 자동으로 재시도**하는 메커니즘을 제공합니다. OrleansX는 **지수 백오프(Exponential Backoff)** 전략을 기본으로 제공합니다.

**핵심 개념**: 네트워크 오류, 타임아웃 등 일시적인 문제를 자동으로 재시도하여 **서비스 안정성 향상**

## 🎯 왜 재시도 정책이 필요한가?

### 분산 시스템의 일시적 오류

```csharp
// ❌ 재시도 없이 호출하면...
public async Task<PlayerData> GetPlayerAsync(string playerId)
{
    var player = _invoker.GetGrain<IPlayerGrain>(playerId);

    try
    {
        return await player.GetDataAsync();
    }
    catch (TimeoutException)
    {
        // 일시적인 네트워크 지연으로 실패
        // 사용자에게 에러 반환... 😞
        throw;
    }
}
```

**발생 가능한 일시적 오류:**
1. **네트워크 지연**: 일시적인 네트워크 혼잡
2. **Grain 활성화 지연**: Grain이 비활성화 상태에서 활성화되는 시간
3. **리소스 경합**: CPU/메모리 일시적 부족
4. **연결 끊김**: Socket 일시 단절

```csharp
// ✅ 재시도 정책 적용
public async Task<PlayerData> GetPlayerAsync(string playerId)
{
    return await RetryAsync(async () =>
    {
        var player = _invoker.GetGrain<IPlayerGrain>(playerId);
        return await player.GetDataAsync();
    });
}

// 1차 시도: 네트워크 타임아웃 → 200ms 대기
// 2차 시도: 성공! ✅
```

## 🔍 ExponentialRetryPolicy 내부 구조

### 지수 백오프 알고리즘

```csharp
// OrleansX.Client/Retry/ExponentialRetryPolicy.cs
public class ExponentialRetryPolicy : IRetryPolicy
{
    private readonly int _maxAttempts;      // 최대 재시도 횟수
    private readonly int _baseDelayMs;      // 기본 지연 시간 (밀리초)
    private readonly int _maxDelayMs;       // 최대 지연 시간 (밀리초)

    public ExponentialRetryPolicy(
        int maxAttempts = 3,
        int baseDelayMs = 200,
        int maxDelayMs = 10000)
    {
        _maxAttempts = maxAttempts;
        _baseDelayMs = baseDelayMs;
        _maxDelayMs = maxDelayMs;
    }

    // 재시도 가능 여부 판단
    public bool ShouldRetry(Exception exception, int attemptNumber)
    {
        // 최대 재시도 횟수 초과
        if (attemptNumber >= _maxAttempts)
            return false;

        // 재시도 가능한 예외 타입만 재시도
        return exception is RetryableException
            || exception is TimeoutException
            || exception is System.Net.Sockets.SocketException
            || exception is System.IO.IOException;
    }

    // 다음 재시도까지 대기 시간 계산
    public TimeSpan GetNextDelay(int attemptNumber)
    {
        if (attemptNumber <= 0)
            return TimeSpan.Zero;

        // 지수 백오프: baseDelay * 2^(attempt-1)
        var delayMs = Math.Min(
            _baseDelayMs * Math.Pow(2, attemptNumber - 1),
            _maxDelayMs
        );

        // 지터(Jitter) 추가: 랜덤하게 ±20%
        var jitter = Random.Shared.NextDouble() * 0.4 - 0.2;
        delayMs *= (1 + jitter);

        return TimeSpan.FromMilliseconds(delayMs);
    }
}
```

### 지수 백오프 계산 예제

```
기본 설정: baseDelay=200ms, maxDelay=10000ms

1차 시도 실패:
  delayMs = 200 * 2^(1-1) = 200 * 1 = 200ms
  지터 적용 (±20%) → 160ms ~ 240ms
  대기 후 2차 시도

2차 시도 실패:
  delayMs = 200 * 2^(2-1) = 200 * 2 = 400ms
  지터 적용 → 320ms ~ 480ms
  대기 후 3차 시도

3차 시도 실패:
  delayMs = 200 * 2^(3-1) = 200 * 4 = 800ms
  지터 적용 → 640ms ~ 960ms
  대기 후 4차 시도

4차 시도 실패:
  delayMs = 200 * 2^(4-1) = 200 * 8 = 1600ms
  ...

10차 시도 실패:
  delayMs = 200 * 2^(10-1) = 200 * 512 = 102400ms
  → maxDelay 제한으로 10000ms
```

### 지터(Jitter)의 중요성

```csharp
// ❌ 지터 없이 재시도하면?
// 여러 클라이언트가 동시에 실패 시 동시에 재시도 → 서버 과부하!

Client 1: 실패 → 200ms 대기 → 재시도
Client 2: 실패 → 200ms 대기 → 재시도
Client 3: 실패 → 200ms 대기 → 재시도
...
Client 100: 실패 → 200ms 대기 → 재시도

→ 200ms 후 100개 요청이 동시에 도착! 💥 (Thundering Herd Problem)
```

```csharp
// ✅ 지터 추가 시
Client 1: 실패 → 180ms 대기 → 재시도
Client 2: 실패 → 215ms 대기 → 재시도
Client 3: 실패 → 194ms 대기 → 재시도
...
Client 100: 실패 → 227ms 대기 → 재시도

→ 재시도가 시간적으로 분산됨! ✅
```

## 💻 실전 사용 예제

### 1. 기본 재시도 패턴

```csharp
public class PlayerService
{
    private readonly IGrainInvoker _invoker;
    private readonly IRetryPolicy _retryPolicy;
    private readonly ILogger<PlayerService> _logger;

    public PlayerService(
        IGrainInvoker invoker,
        IRetryPolicy retryPolicy,
        ILogger<PlayerService> logger)
    {
        _invoker = invoker;
        _retryPolicy = retryPolicy;
        _logger = logger;
    }

    public async Task<PlayerData> GetPlayerAsync(string playerId)
    {
        return await ExecuteWithRetryAsync(async () =>
        {
            var player = _invoker.GetGrain<IPlayerGrain>(playerId);
            return await player.GetDataAsync();
        });
    }

    private async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> action)
    {
        int attemptNumber = 0;
        Exception? lastException = null;

        while (true)
        {
            attemptNumber++;

            try
            {
                return await action();
            }
            catch (Exception ex)
            {
                lastException = ex;

                if (!_retryPolicy.ShouldRetry(ex, attemptNumber))
                {
                    _logger.LogError(ex,
                        "Operation failed after {Attempts} attempts",
                        attemptNumber);
                    throw;
                }

                var delay = _retryPolicy.GetNextDelay(attemptNumber);
                _logger.LogWarning(
                    "Attempt {Attempt} failed: {Error}. Retrying in {Delay}ms",
                    attemptNumber, ex.Message, delay.TotalMilliseconds);

                await Task.Delay(delay);
            }
        }
    }
}
```

### 2. 재사용 가능한 재시도 헬퍼

```csharp
public static class RetryHelper
{
    public static async Task<T> ExecuteWithRetryAsync<T>(
        Func<Task<T>> action,
        IRetryPolicy retryPolicy,
        ILogger? logger = null)
    {
        int attemptNumber = 0;

        while (true)
        {
            attemptNumber++;

            try
            {
                return await action();
            }
            catch (Exception ex)
            {
                if (!retryPolicy.ShouldRetry(ex, attemptNumber))
                {
                    logger?.LogError(ex,
                        "Operation failed after {Attempts} attempts",
                        attemptNumber);
                    throw;
                }

                var delay = retryPolicy.GetNextDelay(attemptNumber);
                logger?.LogWarning(
                    "Attempt {Attempt} failed. Retrying in {Delay}ms",
                    attemptNumber, delay.TotalMilliseconds);

                await Task.Delay(delay);
            }
        }
    }

    public static async Task ExecuteWithRetryAsync(
        Func<Task> action,
        IRetryPolicy retryPolicy,
        ILogger? logger = null)
    {
        await ExecuteWithRetryAsync(async () =>
        {
            await action();
            return 0;  // Dummy return
        }, retryPolicy, logger);
    }
}

// 사용
public class GameService
{
    private readonly IRetryPolicy _retryPolicy;
    private readonly ILogger<GameService> _logger;

    public async Task UpdatePlayerAsync(string playerId, PlayerData data)
    {
        await RetryHelper.ExecuteWithRetryAsync(async () =>
        {
            var player = _invoker.GetGrain<IPlayerGrain>(playerId);
            await player.UpdateDataAsync(data);
        }, _retryPolicy, _logger);
    }
}
```

### 3. 커스텀 재시도 정책

```csharp
// 선형 백오프 정책
public class LinearRetryPolicy : IRetryPolicy
{
    private readonly int _maxAttempts;
    private readonly int _delayMs;

    public LinearRetryPolicy(int maxAttempts = 5, int delayMs = 500)
    {
        _maxAttempts = maxAttempts;
        _delayMs = delayMs;
    }

    public bool ShouldRetry(Exception exception, int attemptNumber)
    {
        if (attemptNumber >= _maxAttempts)
            return false;

        return exception is RetryableException
            || exception is TimeoutException;
    }

    public TimeSpan GetNextDelay(int attemptNumber)
    {
        // 선형: 매번 동일한 지연 시간
        return TimeSpan.FromMilliseconds(_delayMs);
    }
}

// 즉시 재시도 정책 (테스트용)
public class ImmediateRetryPolicy : IRetryPolicy
{
    private readonly int _maxAttempts;

    public ImmediateRetryPolicy(int maxAttempts = 3)
    {
        _maxAttempts = maxAttempts;
    }

    public bool ShouldRetry(Exception exception, int attemptNumber)
    {
        return attemptNumber < _maxAttempts &&
               (exception is RetryableException || exception is TimeoutException);
    }

    public TimeSpan GetNextDelay(int attemptNumber)
    {
        return TimeSpan.Zero;  // 즉시 재시도
    }
}

// 조건부 재시도 정책
public class ConditionalRetryPolicy : IRetryPolicy
{
    private readonly int _maxAttempts;
    private readonly int _baseDelayMs;
    private readonly Func<Exception, bool> _retryableExceptionPredicate;

    public ConditionalRetryPolicy(
        Func<Exception, bool> retryableExceptionPredicate,
        int maxAttempts = 3,
        int baseDelayMs = 200)
    {
        _retryableExceptionPredicate = retryableExceptionPredicate;
        _maxAttempts = maxAttempts;
        _baseDelayMs = baseDelayMs;
    }

    public bool ShouldRetry(Exception exception, int attemptNumber)
    {
        return attemptNumber < _maxAttempts &&
               _retryableExceptionPredicate(exception);
    }

    public TimeSpan GetNextDelay(int attemptNumber)
    {
        return TimeSpan.FromMilliseconds(_baseDelayMs * Math.Pow(2, attemptNumber - 1));
    }
}

// 사용
var customPolicy = new ConditionalRetryPolicy(
    ex => ex is TimeoutException ||
          (ex is HttpRequestException httpEx && httpEx.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable),
    maxAttempts: 5,
    baseDelayMs: 300
);
```

### 4. 재시도와 Circuit Breaker 조합

```csharp
public class ResilientGrainInvoker
{
    private readonly IGrainInvoker _invoker;
    private readonly IRetryPolicy _retryPolicy;
    private readonly ILogger _logger;
    private readonly Dictionary<string, CircuitBreakerState> _circuitBreakers = new();

    public async Task<T> CallGrainWithResilienceAsync<T>(
        string grainKey,
        Func<Task<T>> grainCall)
    {
        var breaker = GetOrCreateCircuitBreaker(grainKey);

        if (breaker.IsOpen)
        {
            throw new InvalidOperationException(
                $"Circuit breaker is open for grain {grainKey}");
        }

        try
        {
            var result = await RetryHelper.ExecuteWithRetryAsync(
                grainCall,
                _retryPolicy,
                _logger);

            breaker.RecordSuccess();
            return result;
        }
        catch (Exception ex)
        {
            breaker.RecordFailure();
            throw;
        }
    }

    private CircuitBreakerState GetOrCreateCircuitBreaker(string grainKey)
    {
        if (!_circuitBreakers.TryGetValue(grainKey, out var breaker))
        {
            breaker = new CircuitBreakerState(
                failureThreshold: 5,
                openDuration: TimeSpan.FromSeconds(30));
            _circuitBreakers[grainKey] = breaker;
        }
        return breaker;
    }
}

public class CircuitBreakerState
{
    private int _failureCount;
    private DateTime _openedAt;
    private readonly int _failureThreshold;
    private readonly TimeSpan _openDuration;

    public CircuitBreakerState(int failureThreshold, TimeSpan openDuration)
    {
        _failureThreshold = failureThreshold;
        _openDuration = openDuration;
    }

    public bool IsOpen
    {
        get
        {
            if (_failureCount < _failureThreshold)
                return false;

            // 일정 시간 후 자동 복구
            if (DateTime.UtcNow - _openedAt > _openDuration)
            {
                _failureCount = 0;
                return false;
            }

            return true;
        }
    }

    public void RecordSuccess()
    {
        _failureCount = 0;
    }

    public void RecordFailure()
    {
        _failureCount++;
        if (_failureCount >= _failureThreshold)
        {
            _openedAt = DateTime.UtcNow;
        }
    }
}
```

## 🎮 게임 서버 활용 예제

### 1. 플레이어 데이터 저장 재시도

```csharp
public class PlayerGrain : StatefulGrainBase<PlayerState>, IPlayerGrain
{
    private readonly IRetryPolicy _retryPolicy;

    public PlayerGrain(
        [PersistentState("state")] IPersistentState<PlayerState> state,
        ILogger<PlayerGrain> logger,
        IRetryPolicy retryPolicy)
        : base(state, logger)
    {
        _retryPolicy = retryPolicy;
    }

    public async Task SaveProgressAsync()
    {
        await RetryHelper.ExecuteWithRetryAsync(async () =>
        {
            // 데이터베이스 저장 시 일시적 오류 재시도
            await SaveStateAsync();
        }, _retryPolicy, Logger);
    }
}
```

### 2. 외부 API 호출 재시도

```csharp
public class PaymentService
{
    private readonly HttpClient _httpClient;
    private readonly IRetryPolicy _retryPolicy;
    private readonly ILogger<PaymentService> _logger;

    public async Task<PaymentResult> ProcessPaymentAsync(PaymentRequest request)
    {
        return await RetryHelper.ExecuteWithRetryAsync(async () =>
        {
            // 결제 API 호출 시 네트워크 오류 재시도
            var response = await _httpClient.PostAsJsonAsync("/api/payment", request);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<PaymentResult>();
        }, _retryPolicy, _logger);
    }
}
```

### 3. 분산 트랜잭션 재시도

```csharp
public class TradeService
{
    private readonly IGrainInvoker _invoker;
    private readonly IRetryPolicy _retryPolicy;

    public async Task ExecuteTradeAsync(string sellerId, string buyerId, string itemId, long price)
    {
        await RetryHelper.ExecuteWithRetryAsync(async () =>
        {
            var seller = _invoker.GetGrain<IPlayerGrain>(sellerId);
            var buyer = _invoker.GetGrain<IPlayerGrain>(buyerId);

            // 분산 트랜잭션: 동시성 충돌 시 재시도
            var sellerHasItem = await seller.HasItemAsync(itemId);
            if (!sellerHasItem)
                throw new InvalidOperationException("Seller doesn't have the item");

            var buyerHasGold = await buyer.HasEnoughGoldAsync(price);
            if (!buyerHasGold)
                throw new InvalidOperationException("Buyer doesn't have enough gold");

            await seller.RemoveItemAsync(itemId);
            await seller.AddGoldAsync(price);
            await buyer.AddItemAsync(itemId);
            await buyer.DeductGoldAsync(price);

        }, _retryPolicy);
    }
}
```

## 🔧 설정 및 등록

### 1. DI 컨테이너 등록

```csharp
// Program.cs
builder.Services.AddSingleton<IRetryPolicy>(
    new ExponentialRetryPolicy(
        maxAttempts: 5,
        baseDelayMs: 200,
        maxDelayMs: 10000
    )
);

// 또는 Options 패턴 사용
builder.Services.Configure<RetryOptions>(
    builder.Configuration.GetSection("Retry"));

builder.Services.AddSingleton<IRetryPolicy>(sp =>
{
    var options = sp.GetRequiredService<IOptions<RetryOptions>>().Value;
    return new ExponentialRetryPolicy(
        options.MaxAttempts,
        options.BaseDelayMs,
        options.MaxDelayMs
    );
});
```

### 2. appsettings.json 설정

```json
{
  "Retry": {
    "MaxAttempts": 5,
    "BaseDelayMs": 200,
    "MaxDelayMs": 10000
  },
  "Logging": {
    "LogLevel": {
      "OrleansX.Client.Retry": "Warning"
    }
  }
}
```

### 3. 환경별 설정

```csharp
// 개발 환경: 빠른 재시도
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddSingleton<IRetryPolicy>(
        new ExponentialRetryPolicy(maxAttempts: 3, baseDelayMs: 100, maxDelayMs: 1000)
    );
}
// 프로덕션: 안정적인 재시도
else
{
    builder.Services.AddSingleton<IRetryPolicy>(
        new ExponentialRetryPolicy(maxAttempts: 5, baseDelayMs: 500, maxDelayMs: 30000)
    );
}
```

## ⚠️ 주의사항

### 1. 멱등성(Idempotency) 보장 필수

```csharp
// ❌ 멱등하지 않은 작업은 재시도하면 안 됨!
public async Task AddGoldAsync(long amount)
{
    // 재시도 시 골드가 중복으로 추가됨!
    State.Gold += amount;
    await SaveStateAsync();
}

// ✅ 멱등한 작업으로 변경
public async Task AddGoldAsync(long amount, string idempotencyKey)
{
    if (State.ProcessedKeys.Contains(idempotencyKey))
        return;  // 이미 처리됨

    State.Gold += amount;
    State.ProcessedKeys.Add(idempotencyKey);
    await SaveStateAsync();
}
```

### 2. 재시도하면 안 되는 예외

```csharp
public bool ShouldRetry(Exception exception, int attemptNumber)
{
    // ❌ 재시도하면 안 되는 예외들
    if (exception is ArgumentException ||
        exception is ArgumentNullException ||
        exception is InvalidOperationException ||
        exception is UnauthorizedAccessException)
    {
        return false;  // 로직 오류이므로 재시도 무의미
    }

    // ✅ 재시도 가능한 일시적 오류
    return exception is TimeoutException ||
           exception is RetryableException ||
           exception is SocketException;
}
```

### 3. 무한 재시도 방지

```csharp
// ❌ 위험: 재시도 횟수 제한 없음
while (true)
{
    try
    {
        await action();
        break;
    }
    catch
    {
        await Task.Delay(1000);
        // 무한 루프!
    }
}

// ✅ 안전: 최대 재시도 횟수 제한
for (int i = 0; i < maxAttempts; i++)
{
    try
    {
        await action();
        return;
    }
    catch (Exception ex) when (i < maxAttempts - 1)
    {
        await Task.Delay(GetDelay(i));
    }
}
```

## 📊 성능 영향 분석

### 재시도 오버헤드

```
시나리오 1: 첫 시도 성공 (대부분의 경우)
- 오버헤드: 거의 없음 (예외 처리 없음)
- 응답 시간: 정상 응답 시간

시나리오 2: 2차 시도 성공
- 오버헤드: 200ms 대기 + 1회 추가 호출
- 응답 시간: 정상 + 200ms

시나리오 3: 3차 시도 성공
- 오버헤드: 200ms + 400ms 대기 + 2회 추가 호출
- 응답 시간: 정상 + 600ms

시나리오 4: 모두 실패 (최대 3회)
- 오버헤드: 200ms + 400ms 대기 + 2회 추가 호출
- 응답 시간: 정상 + 600ms + 최종 실패
```

### 권장 설정값

| 환경 | MaxAttempts | BaseDelay | MaxDelay |
|------|-------------|-----------|----------|
| 개발 | 2-3 | 100ms | 1000ms |
| 스테이징 | 3-5 | 200ms | 5000ms |
| 프로덕션 | 5-7 | 500ms | 30000ms |
| 배치 작업 | 10+ | 1000ms | 60000ms |

## 🔗 관련 문서

- [멱등성 (Idempotency)](Idempotency.md) - 재시도 시 필수
- [GrainInvoker](GrainInvoker.md) - Grain 호출 파사드
- [의존성 주입 (DI)](../Advanced/DependencyInjection.md) - IRetryPolicy 등록

## 📚 참고 자료

- [Exponential Backoff](https://en.wikipedia.org/wiki/Exponential_backoff)
- [Polly - .NET Resilience Library](https://github.com/App-vNext/Polly)
- [Azure Retry Guidance](https://learn.microsoft.com/azure/architecture/best-practices/retry-service-specific)
- [Circuit Breaker Pattern](https://martinfowler.com/bliki/CircuitBreaker.html)
