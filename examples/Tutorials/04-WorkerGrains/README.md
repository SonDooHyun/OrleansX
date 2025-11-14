# Tutorial 04: Worker Grains

## 개요

Worker Grain은 주기적인 백그라운드 작업을 수행하는 Grain입니다. Timer를 사용하여 정해진 간격으로 작업을 실행하며, 통계 및 상태 모니터링을 제공합니다.

## 언제 사용하나요?

- 주기적인 데이터 정리 (Cleanup)
- 배치 처리 작업
- 모니터링 및 헬스 체크
- 스케줄링된 알림
- 통계 집계

## Worker Grain 종류

### 1. Stateless Worker Grain
- 상태를 저장하지 않음
- 가벼운 백그라운드 작업에 적합

### 2. Stateful Worker Grain
- 영속적인 상태 저장
- 작업 진행 상황 추적 가능

## 예제 1: Stateless Cleanup Worker

### 1. Grain 인터페이스 정의

```csharp
using Orleans;

namespace OrleansX.Tutorials.WorkerGrains;

public interface ICleanupWorkerGrain : IGrainWithStringKey
{
    Task StartAsync();
    Task StopAsync();
    Task<WorkerStatus> GetStatusAsync();
}
```

### 2. Grain 구현

```csharp
using Microsoft.Extensions.Logging;
using OrleansX.Grains;

namespace OrleansX.Tutorials.WorkerGrains;

public class CleanupWorkerGrain : StatelessWorkerGrainBase, ICleanupWorkerGrain
{
    public CleanupWorkerGrain(ILogger<CleanupWorkerGrain> logger)
        : base(logger)
    {
    }

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        await base.OnActivateAsync(cancellationToken);

        // Grain 활성화 시 자동으로 Worker 시작
        // 1분 후 첫 실행, 이후 30분마다 실행
        await StartTimerAsync(TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(30));
    }

    public Task StartAsync()
    {
        return StartTimerAsync(TimeSpan.Zero, TimeSpan.FromMinutes(30));
    }

    public new Task StopAsync()
    {
        return base.StopAsync();
    }

    public new Task<WorkerStatus> GetStatusAsync()
    {
        return base.GetStatusAsync();
    }

    protected override async Task ExecuteWorkAsync()
    {
        Logger.LogInformation("Starting cleanup work at {Time}", DateTime.UtcNow);

        // 실제 정리 작업 수행
        await CleanupExpiredSessionsAsync();
        await CleanupOldLogsAsync();
        await CleanupTempFilesAsync();

        Logger.LogInformation("Cleanup work completed at {Time}", DateTime.UtcNow);
    }

    private async Task CleanupExpiredSessionsAsync()
    {
        // 만료된 세션 정리 로직
        await Task.Delay(100); // 시뮬레이션
        Logger.LogInformation("Expired sessions cleaned up");
    }

    private async Task CleanupOldLogsAsync()
    {
        // 오래된 로그 정리 로직
        await Task.Delay(100); // 시뮬레이션
        Logger.LogInformation("Old logs cleaned up");
    }

    private async Task CleanupTempFilesAsync()
    {
        // 임시 파일 정리 로직
        await Task.Delay(100); // 시뮬레이션
        Logger.LogInformation("Temp files cleaned up");
    }

    protected override Task OnErrorAsync(Exception exception)
    {
        Logger.LogError(exception, "Error occurred during cleanup work");
        // 에러 발생 시 알림 등 추가 처리 가능
        return Task.CompletedTask;
    }
}
```

## 예제 2: Stateful Statistics Worker

### 1. 상태 모델 정의

```csharp
using Orleans;

namespace OrleansX.Tutorials.WorkerGrains;

[GenerateSerializer]
public class StatisticsState
{
    [Id(0)]
    public long TotalProcessed { get; set; }

    [Id(1)]
    public DateTime LastAggregationTime { get; set; }

    [Id(2)]
    public Dictionary<string, long> CountersByType { get; set; } = new();
}
```

### 2. Grain 인터페이스

```csharp
using Orleans;

namespace OrleansX.Tutorials.WorkerGrains;

public interface IStatisticsWorkerGrain : IGrainWithStringKey
{
    Task StartAsync();
    Task StopAsync();
    Task<WorkerStatus> GetStatusAsync();
    Task<StatisticsReport> GetReportAsync();
}

[GenerateSerializer]
public class StatisticsReport
{
    [Id(0)]
    public long TotalProcessed { get; set; }

    [Id(1)]
    public DateTime LastAggregationTime { get; set; }

    [Id(2)]
    public Dictionary<string, long> CountersByType { get; set; } = new();

    [Id(3)]
    public bool IsRunning { get; set; }
}
```

### 3. Grain 구현

```csharp
using Microsoft.Extensions.Logging;
using Orleans.Runtime;
using OrleansX.Grains;

namespace OrleansX.Tutorials.WorkerGrains;

public class StatisticsWorkerGrain : StatefulWorkerGrainBase<StatisticsState>, IStatisticsWorkerGrain
{
    public StatisticsWorkerGrain(
        [PersistentState("statistics")] IPersistentState<StatisticsState> state,
        ILogger<StatisticsWorkerGrain> logger)
        : base(state, logger)
    {
    }

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        await base.OnActivateAsync(cancellationToken);

        // 5분마다 통계 집계
        await StartTimerAsync(TimeSpan.FromSeconds(10), TimeSpan.FromMinutes(5));
    }

    public Task StartAsync()
    {
        return StartTimerAsync(TimeSpan.Zero, TimeSpan.FromMinutes(5));
    }

    public new Task StopAsync()
    {
        return base.StopAsync();
    }

    public new Task<WorkerStatus> GetStatusAsync()
    {
        return base.GetStatusAsync();
    }

    public Task<StatisticsReport> GetReportAsync()
    {
        return Task.FromResult(new StatisticsReport
        {
            TotalProcessed = State.TotalProcessed,
            LastAggregationTime = State.LastAggregationTime,
            CountersByType = new Dictionary<string, long>(State.CountersByType),
            IsRunning = IsRunning
        });
    }

    protected override async Task ExecuteWorkAsync()
    {
        Logger.LogInformation("Aggregating statistics at {Time}", DateTime.UtcNow);

        // 통계 집계 로직
        var newStats = await CollectStatisticsAsync();

        // 상태 업데이트
        State.TotalProcessed += newStats.Total;
        State.LastAggregationTime = DateTime.UtcNow;

        foreach (var (type, count) in newStats.ByType)
        {
            if (State.CountersByType.ContainsKey(type))
                State.CountersByType[type] += count;
            else
                State.CountersByType[type] = count;
        }

        await SaveStateAsync();

        Logger.LogInformation("Statistics aggregated. Total: {Total}", State.TotalProcessed);
    }

    private async Task<(long Total, Dictionary<string, long> ByType)> CollectStatisticsAsync()
    {
        // 실제 통계 수집 로직
        await Task.Delay(100); // 시뮬레이션

        return (
            Total: 100,
            ByType: new Dictionary<string, long>
            {
                ["Login"] = 45,
                ["Purchase"] = 30,
                ["Search"] = 25
            }
        );
    }
}
```

### 4. 클라이언트에서 사용

```csharp
using OrleansX.Abstractions;

namespace OrleansX.Tutorials.WorkerGrains;

public class WorkerManagementService
{
    private readonly IGrainInvoker _invoker;

    public WorkerManagementService(IGrainInvoker invoker)
    {
        _invoker = invoker;
    }

    public async Task ManageCleanupWorkerAsync()
    {
        var worker = _invoker.GetGrain<ICleanupWorkerGrain>("cleanup-worker");

        // Worker 시작
        await worker.StartAsync();
        Console.WriteLine("Cleanup worker started");

        // 상태 확인
        await Task.Delay(5000);
        var status = await worker.GetStatusAsync();
        Console.WriteLine($"Worker Status:");
        Console.WriteLine($"  IsRunning: {status.IsRunning}");
        Console.WriteLine($"  Success Count: {status.SuccessCount}");
        Console.WriteLine($"  Failure Count: {status.FailureCount}");
        Console.WriteLine($"  Last Execution: {status.LastExecutionTime}");
        Console.WriteLine($"  Next Execution: {status.NextExecutionTime}");

        // Worker 중지
        await worker.StopAsync();
        Console.WriteLine("Cleanup worker stopped");
    }

    public async Task MonitorStatisticsWorkerAsync()
    {
        var worker = _invoker.GetGrain<IStatisticsWorkerGrain>("stats-worker");

        // Worker는 자동으로 시작됨 (OnActivateAsync)

        // 통계 조회
        var report = await worker.GetReportAsync();
        Console.WriteLine($"Statistics Report:");
        Console.WriteLine($"  Total Processed: {report.TotalProcessed}");
        Console.WriteLine($"  Last Aggregation: {report.LastAggregationTime}");
        Console.WriteLine($"  Is Running: {report.IsRunning}");
        Console.WriteLine($"  Counters:");
        foreach (var (type, count) in report.CountersByType)
        {
            Console.WriteLine($"    {type}: {count}");
        }
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
            options.AddMemoryGrainStorage("statistics");
        });
    });

await builder.Build().RunAsync();
```

## 주요 특징

### 1. 자동 에러 처리
```csharp
protected override Task OnErrorAsync(Exception exception)
{
    // 에러 발생 시에도 Worker는 계속 실행됨
    Logger.LogError(exception, "Work failed");
    return Task.CompletedTask;
}
```

### 2. 실행 통계
```csharp
var status = await worker.GetStatusAsync();
// IsRunning, LastExecutionTime, NextExecutionTime
// SuccessCount, FailureCount
```

### 3. 생명주기 훅
```csharp
protected override Task OnBeforeExecuteAsync()
{
    // 작업 시작 전
    return Task.CompletedTask;
}

protected override Task OnAfterExecuteAsync()
{
    // 작업 완료 후
    return Task.CompletedTask;
}
```

## 실행 예제

```bash
# Silo 실행
cd Tutorials/04-WorkerGrains/SiloHost
dotnet run

# 별도 터미널에서 클라이언트 실행
cd Tutorials/04-WorkerGrains/Client
dotnet run
```

## 예상 출력

```
Cleanup worker started
Worker Status:
  IsRunning: True
  Success Count: 1
  Failure Count: 0
  Last Execution: 2025-01-15 10:30:00
  Next Execution: 2025-01-15 11:00:00
Cleanup worker stopped

Statistics Report:
  Total Processed: 500
  Last Aggregation: 2025-01-15 10:35:00
  Is Running: True
  Counters:
    Login: 225
    Purchase: 150
    Search: 125
```

## Best Practices

### 1. 작업 시간 고려
```csharp
// ✅ 짧은 작업 - Timer 사용
await StartTimerAsync(TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));

// ❌ 긴 작업 (30분 이상) - Reminder 고려
// Timer는 Grain 비활성화 시 중지됨
```

### 2. 에러 처리
```csharp
protected override async Task ExecuteWorkAsync()
{
    try
    {
        await DoWorkAsync();
    }
    catch (SpecificException ex)
    {
        // 특정 예외는 여기서 처리
        Logger.LogWarning(ex, "Recoverable error");
    }
    // 나머지는 OnErrorAsync로 전달됨
}
```

### 3. 상태 저장 주기
```csharp
// ✅ 중요한 작업 후 저장
State.ProcessedCount += batch.Count;
await SaveStateAsync();

// ❌ 너무 자주 저장하지 않기
// 루프 내에서 매번 저장하면 성능 저하
```

## Reminder vs Timer

| 기능 | Timer | Reminder |
|------|-------|----------|
| Grain 비활성화 시 | 중지됨 | 계속 실행 |
| 영속성 | 없음 | 있음 |
| 오버헤드 | 낮음 | 높음 |
| 사용 사례 | 짧은 주기 작업 | 긴 주기 작업 |

## 다음 단계

- [Tutorial 05: Client SDK](../05-ClientSDK/README.md) - 고급 클라이언트 기능 학습
- [Tutorial 06: Streams](../06-Streams/README.md) - 실시간 데이터 스트림 학습
