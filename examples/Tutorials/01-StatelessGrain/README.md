# Tutorial 01: Stateless Grain

## 개요

Stateless Grain은 상태를 저장하지 않는 가장 간단한 형태의 Grain입니다. 각 호출마다 독립적으로 처리되며, 여러 인스턴스가 동시에 활성화될 수 있어 높은 처리량을 제공합니다.

## 언제 사용하나요?

- 상태를 저장할 필요가 없는 계산 작업
- 외부 API 호출
- 데이터 변환 및 검증
- 캐시 레이어

## 예제: 간단한 계산기 Grain

### 1. Grain 인터페이스 정의

```csharp
using Orleans;

namespace OrleansX.Tutorials.StatelessGrain;

public interface ICalculatorGrain : IGrainWithStringKey
{
    Task<int> AddAsync(int a, int b);
    Task<int> MultiplyAsync(int a, int b);
    Task<double> DivideAsync(double a, double b);
}
```

### 2. Grain 구현

```csharp
using Microsoft.Extensions.Logging;
using OrleansX.Grains;

namespace OrleansX.Tutorials.StatelessGrain;

public class CalculatorGrain : StatelessGrainBase, ICalculatorGrain
{
    public CalculatorGrain(ILogger<CalculatorGrain> logger)
        : base(logger)
    {
    }

    public Task<int> AddAsync(int a, int b)
    {
        Logger.LogInformation("Adding {A} + {B}", a, b);
        return Task.FromResult(a + b);
    }

    public Task<int> MultiplyAsync(int a, int b)
    {
        Logger.LogInformation("Multiplying {A} * {B}", a, b);
        return Task.FromResult(a * b);
    }

    public Task<double> DivideAsync(double a, double b)
    {
        if (b == 0)
        {
            Logger.LogError("Division by zero attempted");
            throw new DivideByZeroException("Cannot divide by zero");
        }

        Logger.LogInformation("Dividing {A} / {B}", a, b);
        return Task.FromResult(a / b);
    }
}
```

### 3. 클라이언트에서 사용

```csharp
using OrleansX.Abstractions;

namespace OrleansX.Tutorials.StatelessGrain;

public class CalculatorService
{
    private readonly IGrainInvoker _invoker;

    public CalculatorService(IGrainInvoker invoker)
    {
        _invoker = invoker;
    }

    public async Task<int> PerformCalculationAsync()
    {
        var calculator = _invoker.GetGrain<ICalculatorGrain>("calculator");

        var sum = await calculator.AddAsync(10, 20);
        Console.WriteLine($"10 + 20 = {sum}");

        var product = await calculator.MultiplyAsync(5, 6);
        Console.WriteLine($"5 * 6 = {product}");

        var quotient = await calculator.DivideAsync(100, 4);
        Console.WriteLine($"100 / 4 = {quotient}");

        return sum;
    }
}
```

## 실행 방법

### Silo 구성

```csharp
var builder = Host.CreateDefaultBuilder(args)
    .UseOrleans((context, siloBuilder) =>
    {
        siloBuilder.UseOrleansX(options =>
        {
            options.UseLocalhostClustering(siloPort: 11111, gatewayPort: 30000);
        });
    });

await builder.Build().RunAsync();
```

### 클라이언트 구성

```csharp
var builder = Host.CreateDefaultBuilder(args)
    .UseOrleansClient((context, clientBuilder) =>
    {
        clientBuilder.UseLocalhostClustering(gatewayPort: 30000);
    })
    .ConfigureServices(services =>
    {
        services.AddOrleansXClient();
        services.AddScoped<CalculatorService>();
    });

var host = builder.Build();
await host.StartAsync();

var service = host.Services.GetRequiredService<CalculatorService>();
await service.PerformCalculationAsync();
```

## 주요 특징

### 1. Stateless 특성
- 각 호출이 독립적입니다
- 여러 인스턴스가 동시에 활성화될 수 있습니다
- 높은 처리량 제공

### 2. 로깅
- `StatelessGrainBase`는 `ILogger`를 제공합니다
- 모든 작업을 로깅하여 디버깅을 쉽게 합니다

### 3. 예외 처리
- Orleans는 예외를 자동으로 클라이언트에 전파합니다
- 도메인 예외를 정의하여 명확한 에러 처리 가능

## 실행 예제

```bash
# Silo 실행
cd Tutorials/01-StatelessGrain/SiloHost
dotnet run

# 별도 터미널에서 클라이언트 실행
cd Tutorials/01-StatelessGrain/Client
dotnet run
```

## 예상 출력

```
10 + 20 = 30
5 * 6 = 30
100 / 4 = 25
```

## 다음 단계

- [Tutorial 02: Stateful Grain](../02-StatefulGrain/README.md) - 상태를 가진 Grain 학습
- [Tutorial 05: Client SDK](../05-ClientSDK/README.md) - 고급 클라이언트 기능 학습
