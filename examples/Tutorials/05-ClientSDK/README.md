# Tutorial 05: Client SDK

## 개요

OrleansX Client SDK는 Grain 호출을 단순화하고, 재시도 정책, 멱등성 관리 등의 고급 기능을 제공합니다.

## 핵심 구성요소

### 1. IGrainInvoker
Grain 호출을 위한 파사드 인터페이스

### 2. Retry Policy
자동 재시도 정책

### 3. Idempotency Key Provider
멱등성 키 관리

## 기본 사용법

### 1. 클라이언트 설정

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateDefaultBuilder(args)
    .UseOrleansClient((context, clientBuilder) =>
    {
        clientBuilder.UseLocalhostClustering(gatewayPort: 30000);
    })
    .ConfigureServices(services =>
    {
        // OrleansX Client SDK 등록
        services.AddOrleansXClient();
    });

var host = builder.Build();
await host.StartAsync();
```

### 2. IGrainInvoker 사용

```csharp
using OrleansX.Abstractions;

namespace OrleansX.Tutorials.ClientSDK;

public class UserService
{
    private readonly IGrainInvoker _invoker;

    public UserService(IGrainInvoker invoker)
    {
        _invoker = invoker;
    }

    public async Task<UserInfo> GetUserAsync(string userId)
    {
        // Grain 참조 획득
        var grain = _invoker.GetGrain<IUserGrain>(userId);

        // Grain 메서드 호출
        return await grain.GetInfoAsync();
    }

    public async Task UpdateUserAsync(string userId, string name)
    {
        var grain = _invoker.GetGrain<IUserGrain>(userId);
        await grain.UpdateNameAsync(name);
    }
}
```

## 재시도 정책 (Retry Policy)

### 1. 기본 Exponential Retry

```csharp
using OrleansX.Abstractions;
using OrleansX.Client.Retry;

namespace OrleansX.Tutorials.ClientSDK;

public class ResilientService
{
    private readonly IGrainInvoker _invoker;
    private readonly IRetryPolicy _retryPolicy;

    public ResilientService(IGrainInvoker invoker)
    {
        _invoker = invoker;

        // Exponential Backoff 재시도 정책
        _retryPolicy = new ExponentialRetryPolicy(
            maxRetries: 3,
            initialDelay: TimeSpan.FromMilliseconds(100),
            maxDelay: TimeSpan.FromSeconds(5)
        );
    }

    public async Task<OrderInfo> PlaceOrderAsync(string orderId, OrderRequest request)
    {
        var grain = _invoker.GetGrain<IOrderGrain>(orderId);

        return await _retryPolicy.ExecuteAsync(async () =>
        {
            return await grain.PlaceOrderAsync(request);
        });
    }
}
```

### 2. 커스텀 Retry Policy

```csharp
using OrleansX.Abstractions;

namespace OrleansX.Tutorials.ClientSDK;

public class CustomRetryPolicy : IRetryPolicy
{
    private readonly int _maxRetries;
    private readonly TimeSpan _delay;

    public CustomRetryPolicy(int maxRetries = 3, TimeSpan? delay = null)
    {
        _maxRetries = maxRetries;
        _delay = delay ?? TimeSpan.FromSeconds(1);
    }

    public async Task<T> ExecuteAsync<T>(Func<Task<T>> action)
    {
        int attempt = 0;
        Exception? lastException = null;

        while (attempt < _maxRetries)
        {
            try
            {
                return await action();
            }
            catch (Exception ex) when (IsTransient(ex))
            {
                lastException = ex;
                attempt++;

                if (attempt < _maxRetries)
                {
                    await Task.Delay(_delay * attempt); // Linear backoff
                }
            }
        }

        throw new InvalidOperationException(
            $"Failed after {_maxRetries} attempts", lastException);
    }

    private bool IsTransient(Exception ex)
    {
        // 재시도 가능한 예외 판단
        return ex is TimeoutException ||
               ex is TaskCanceledException ||
               ex.Message.Contains("temporarily unavailable");
    }
}
```

### 3. 재시도 정책 사용 예제

```csharp
namespace OrleansX.Tutorials.ClientSDK;

public class PaymentService
{
    private readonly IGrainInvoker _invoker;
    private readonly IRetryPolicy _retryPolicy;

    public PaymentService(IGrainInvoker invoker)
    {
        _invoker = invoker;
        _retryPolicy = new ExponentialRetryPolicy(
            maxRetries: 5,
            initialDelay: TimeSpan.FromMilliseconds(200),
            maxDelay: TimeSpan.FromSeconds(10)
        );
    }

    public async Task<PaymentResult> ProcessPaymentAsync(
        string paymentId,
        decimal amount)
    {
        var grain = _invoker.GetGrain<IPaymentGrain>(paymentId);

        try
        {
            return await _retryPolicy.ExecuteAsync(async () =>
            {
                return await grain.ProcessAsync(amount);
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Payment failed after retries: {ex.Message}");
            throw;
        }
    }
}
```

## 멱등성 (Idempotency)

### 1. AsyncLocal Idempotency Key Provider

```csharp
using OrleansX.Client.Idempotency;

namespace OrleansX.Tutorials.ClientSDK;

public class IdempotentService
{
    private readonly IGrainInvoker _invoker;
    private readonly AsyncLocalIdempotencyKeyProvider _keyProvider;

    public IdempotentService(IGrainInvoker invoker)
    {
        _invoker = invoker;
        _keyProvider = new AsyncLocalIdempotencyKeyProvider();
    }

    public async Task CreateOrderAsync(string orderId, OrderRequest request)
    {
        // 멱등성 키 설정
        var idempotencyKey = Guid.NewGuid().ToString();
        _keyProvider.SetKey(idempotencyKey);

        try
        {
            var grain = _invoker.GetGrain<IOrderGrain>(orderId);

            // 동일한 키로 여러 번 호출해도 한 번만 처리됨
            await grain.CreateAsync(request);

            Console.WriteLine($"Order created with idempotency key: {idempotencyKey}");
        }
        finally
        {
            _keyProvider.ClearKey();
        }
    }

    public async Task SafeRetryCreateOrderAsync(string orderId, OrderRequest request)
    {
        var idempotencyKey = Guid.NewGuid().ToString();
        _keyProvider.SetKey(idempotencyKey);

        try
        {
            var grain = _invoker.GetGrain<IOrderGrain>(orderId);

            // 재시도 시에도 같은 idempotency key 사용
            for (int i = 0; i < 3; i++)
            {
                try
                {
                    await grain.CreateAsync(request);
                    break;
                }
                catch (Exception ex)
                {
                    if (i == 2) throw;
                    Console.WriteLine($"Retry {i + 1}: {ex.Message}");
                    await Task.Delay(1000);
                }
            }
        }
        finally
        {
            _keyProvider.ClearKey();
        }
    }
}
```

## 고급 패턴

### 1. Grain Factory 패턴

```csharp
namespace OrleansX.Tutorials.ClientSDK;

public interface IGrainFactory<TGrain> where TGrain : IGrain
{
    TGrain GetGrain(string key);
}

public class GrainFactory<TGrain> : IGrainFactory<TGrain>
    where TGrain : IGrainWithStringKey
{
    private readonly IGrainInvoker _invoker;

    public GrainFactory(IGrainInvoker invoker)
    {
        _invoker = invoker;
    }

    public TGrain GetGrain(string key)
    {
        return _invoker.GetGrain<TGrain>(key);
    }
}

// 사용 예
public class OrderService
{
    private readonly IGrainFactory<IOrderGrain> _orderFactory;

    public OrderService(IGrainFactory<IOrderGrain> orderFactory)
    {
        _orderFactory = orderFactory;
    }

    public async Task ProcessOrderAsync(string orderId)
    {
        var grain = _orderFactory.GetGrain(orderId);
        await grain.ProcessAsync();
    }
}
```

### 2. Batch Operations

```csharp
namespace OrleansX.Tutorials.ClientSDK;

public class BatchOperationService
{
    private readonly IGrainInvoker _invoker;

    public BatchOperationService(IGrainInvoker invoker)
    {
        _invoker = invoker;
    }

    public async Task<List<UserInfo>> GetMultipleUsersAsync(List<string> userIds)
    {
        // 병렬로 여러 Grain 호출
        var tasks = userIds.Select(async userId =>
        {
            var grain = _invoker.GetGrain<IUserGrain>(userId);
            return await grain.GetInfoAsync();
        });

        var results = await Task.WhenAll(tasks);
        return results.ToList();
    }

    public async Task UpdateMultipleUsersAsync(
        Dictionary<string, string> userUpdates)
    {
        // 병렬 업데이트
        var tasks = userUpdates.Select(async kvp =>
        {
            var grain = _invoker.GetGrain<IUserGrain>(kvp.Key);
            await grain.UpdateNameAsync(kvp.Value);
        });

        await Task.WhenAll(tasks);
    }
}
```

### 3. Circuit Breaker 패턴

```csharp
namespace OrleansX.Tutorials.ClientSDK;

public class CircuitBreakerService
{
    private readonly IGrainInvoker _invoker;
    private int _failureCount = 0;
    private DateTime? _lastFailureTime;
    private const int FailureThreshold = 5;
    private readonly TimeSpan _openDuration = TimeSpan.FromMinutes(1);

    public CircuitBreakerService(IGrainInvoker invoker)
    {
        _invoker = invoker;
    }

    public async Task<T> ExecuteWithCircuitBreakerAsync<T>(
        string grainKey,
        Func<IGrain, Task<T>> action)
    {
        // Circuit이 열려있는지 확인
        if (IsCircuitOpen())
        {
            throw new InvalidOperationException("Circuit breaker is open");
        }

        try
        {
            var grain = _invoker.GetGrain<IGrainWithStringKey>(grainKey);
            var result = await action(grain);

            // 성공 시 failure count 리셋
            _failureCount = 0;
            _lastFailureTime = null;

            return result;
        }
        catch (Exception)
        {
            _failureCount++;
            _lastFailureTime = DateTime.UtcNow;
            throw;
        }
    }

    private bool IsCircuitOpen()
    {
        if (_failureCount >= FailureThreshold)
        {
            if (_lastFailureTime.HasValue &&
                DateTime.UtcNow - _lastFailureTime.Value < _openDuration)
            {
                return true;
            }

            // Circuit 리셋 (Half-Open)
            _failureCount = 0;
            _lastFailureTime = null;
        }

        return false;
    }
}
```

## 실행 방법

### 전체 애플리케이션 설정

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateDefaultBuilder(args)
    .UseOrleansClient((context, clientBuilder) =>
    {
        clientBuilder.UseLocalhostClustering(gatewayPort: 30000);
    })
    .ConfigureServices(services =>
    {
        // OrleansX Client SDK
        services.AddOrleansXClient();

        // 서비스 등록
        services.AddScoped<UserService>();
        services.AddScoped<ResilientService>();
        services.AddScoped<IdempotentService>();
        services.AddScoped<BatchOperationService>();

        // Factory 등록
        services.AddScoped(typeof(IGrainFactory<>), typeof(GrainFactory<>));
    });

var host = builder.Build();
await host.StartAsync();

// 서비스 사용
var userService = host.Services.GetRequiredService<UserService>();
var user = await userService.GetUserAsync("user-123");
Console.WriteLine($"User: {user.Name}");
```

## 실행 예제

```bash
# Silo 실행
cd Tutorials/05-ClientSDK/SiloHost
dotnet run

# 별도 터미널에서 클라이언트 실행
cd Tutorials/05-ClientSDK/Client
dotnet run
```

## 예상 출력

```
User: John Doe
Order created with idempotency key: a1b2c3d4-e5f6-7890-abcd-ef1234567890
Payment processing attempt 1...
Payment processing attempt 2...
Payment successful!
Batch operation: 10 users retrieved
```

## Best Practices

### 1. 재시도 정책 선택
```csharp
// ✅ 멱등성 작업 - 재시도 적극 사용
await retryPolicy.ExecuteAsync(() => grain.GetAsync());

// ❌ 비멱등성 작업 - 재시도 신중하게
// 결제 등은 멱등성 키와 함께 사용
```

### 2. 타임아웃 설정
```csharp
// Grain 호출 타임아웃
var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
await grain.LongRunningOperationAsync(cts.Token);
```

### 3. 에러 처리
```csharp
try
{
    await grain.OperationAsync();
}
catch (GrainOperationException ex)
{
    // Grain에서 발생한 비즈니스 예외
    Console.WriteLine($"Business error: {ex.Message}");
}
catch (OrleansException ex)
{
    // Orleans 인프라 예외
    Console.WriteLine($"Infrastructure error: {ex.Message}");
}
```

## 다음 단계

- [Tutorial 06: Streams](../06-Streams/README.md) - 실시간 데이터 스트림 학습
- [Tutorial 02: Stateful Grain](../02-StatefulGrain/README.md) - Grain 구현 복습
