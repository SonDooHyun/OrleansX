# WorkerExecutor 심층 가이드

## 📖 개요

`WorkerExecutor`는 OrleansX의 **내부 헬퍼 클래스**로, Worker Grain들(`StatelessWorkerGrainBase`, `StatefulWorkerGrainBase`)의 공통 로직을 캡슐화합니다.

**핵심 목표**: 상속 계층의 복잡도를 증가시키지 않으면서 코드 중복을 제거

## 🎯 왜 WorkerExecutor를 만들었나?

### 리팩토링 이전 문제점

```csharp
// ❌ 리팩토링 전: StatelessWorkerGrainBase
public abstract class StatelessWorkerGrainBase : Grain
{
    private IDisposable? _timer;
    private bool _isRunning;
    private DateTime? _lastExecutionTime;
    private DateTime? _nextExecutionTime;
    private int _successCount;
    private int _failureCount;
    private readonly object _statsLock = new();

    protected virtual Task StartTimerAsync(TimeSpan dueTime, TimeSpan period)
    {
        if (_isRunning)
        {
            Logger.LogWarning("Already running");
            return Task.CompletedTask;
        }

        _timer = RegisterGrainTimer(
            async _ => await ExecuteWorkWithErrorHandlingAsync(),
            new GrainTimerCreationOptions { ... });

        _isRunning = true;
        // ... 100+ 줄의 코드
    }

    private async Task ExecuteWorkWithErrorHandlingAsync()
    {
        // ... 에러 처리 로직
        // ... 통계 업데이트 로직
        // ... 로깅 로직
    }

    public virtual Task<WorkerStatus> GetStatusAsync() { ... }
    public virtual Task ResetStatisticsAsync() { ... }
    public Task StopAsync() { ... }
}
```

```csharp
// ❌ 리팩토링 전: StatefulWorkerGrainBase
public abstract class StatefulWorkerGrainBase<TState> : Grain
{
    // 위와 동일한 100+ 줄의 코드 중복!!!
    private IDisposable? _timer;
    private bool _isRunning;
    private DateTime? _lastExecutionTime;
    // ... (완전히 중복된 코드)

    protected virtual Task StartTimerAsync(TimeSpan dueTime, TimeSpan period) { ... }
    private async Task ExecuteWorkWithErrorHandlingAsync() { ... }
    public virtual Task<WorkerStatus> GetStatusAsync() { ... }
    // ... (완전히 중복된 코드)
}
```

**문제점:**
1. 200+ 줄의 코드가 두 클래스에 완전히 중복됨
2. 버그 수정 시 두 곳 모두 수정해야 함
3. 기능 추가 시 두 곳 모두 변경 필요
4. 유지보수 비용 2배 증가

### 리팩토링 후 해결책

```csharp
// ✅ 리팩토링 후: WorkerExecutor에 공통 로직 캡슐화
internal class WorkerExecutor
{
    // 모든 공통 로직을 하나의 클래스에 집중
    private IDisposable? _timer;
    private bool _isRunning;
    private DateTime? _lastExecutionTime;
    // ...

    public Task StartTimerAsync(Grain grain, TimeSpan dueTime, TimeSpan period) { ... }
    private async Task ExecuteWorkWithErrorHandlingAsync() { ... }
    public WorkerStatus GetStatus() { ... }
    // ... (공통 로직 200+ 줄)
}

// ✅ StatelessWorkerGrainBase: 단순히 WorkerExecutor에 위임
public abstract class StatelessWorkerGrainBase : Grain
{
    private readonly WorkerExecutor _executor;

    protected StatelessWorkerGrainBase(ILogger logger)
    {
        _executor = new WorkerExecutor(
            logger,
            ExecuteWorkAsync,           // 실행할 작업
            OnBeforeExecuteAsync,       // 전처리 훅
            OnAfterExecuteAsync,        // 후처리 훅
            OnErrorAsync,               // 에러 처리 훅
            () => this.GetPrimaryKeyString());
    }

    protected virtual Task StartTimerAsync(TimeSpan dueTime, TimeSpan period)
        => _executor.StartTimerAsync(this, dueTime, period);  // 단순 위임

    public virtual Task<WorkerStatus> GetStatusAsync()
        => Task.FromResult(_executor.GetStatus());  // 단순 위임

    // 중복 제거! 이제 20줄 정도로 간결해짐
}

// ✅ StatefulWorkerGrainBase: 동일하게 WorkerExecutor에 위임
public abstract class StatefulWorkerGrainBase<TState> : Grain
{
    private readonly WorkerExecutor _executor;

    // 동일한 패턴으로 중복 제거!
    protected virtual Task StartTimerAsync(TimeSpan dueTime, TimeSpan period)
        => _executor.StartTimerAsync(this, dueTime, period);
}
```

**장점:**
1. **코드 중복 제거**: 200+ 줄 → 20줄로 감소
2. **단일 책임 원칙**: WorkerExecutor는 오직 작업 실행 관리만 담당
3. **유지보수성 향상**: 버그 수정 시 WorkerExecutor만 수정하면 됨
4. **테스트 용이성**: WorkerExecutor를 독립적으로 테스트 가능
5. **상속 계층 단순화**: 다중 상속 문제 없이 기능 공유

## 🔍 WorkerExecutor 내부 구조

### 핵심 설계: 전략 패턴 + 위임

```csharp
// OrleansX.Grains/Internal/WorkerExecutor.cs
internal class WorkerExecutor
{
    // 1. 의존성 주입받은 전략들 (Func<Task> 델리게이트)
    private readonly Func<Task> _executeWork;         // 실행할 작업
    private readonly Func<Task> _onBeforeExecute;     // 전처리
    private readonly Func<Task> _onAfterExecute;      // 후처리
    private readonly Func<Exception, Task> _onError;  // 에러 처리
    private readonly Func<string> _getGrainId;        // Grain ID 조회

    // 2. 상태 관리 필드
    private IDisposable? _timer;
    private bool _isRunning;
    private DateTime? _lastExecutionTime;
    private DateTime? _nextExecutionTime;
    private int _successCount;
    private int _failureCount;
    private readonly object _statsLock = new();

    // 3. 생성자: 모든 전략을 주입받음
    public WorkerExecutor(
        ILogger logger,
        Func<Task> executeWork,          // ← 각 Grain에서 구현한 메서드
        Func<Task> onBeforeExecute,      // ← 각 Grain에서 구현한 훅
        Func<Task> onAfterExecute,       // ← 각 Grain에서 구현한 훅
        Func<Exception, Task> onError,   // ← 각 Grain에서 구현한 훅
        Func<string> getGrainId)         // ← Grain ID 추출 방법
    {
        _executeWork = executeWork;
        _onBeforeExecute = onBeforeExecute;
        _onAfterExecute = onAfterExecute;
        _onError = onError;
        _getGrainId = getGrainId;
    }
}
```

### 실행 흐름

```
1. Grain에서 StartTimerAsync() 호출
   ↓
2. WorkerExecutor.StartTimerAsync(grain, dueTime, period)
   ↓
3. Grain.RegisterGrainTimer()로 타이머 등록
   │   콜백: ExecuteWorkWithErrorHandlingAsync
   ↓
4. 타이머 주기마다 ExecuteWorkWithErrorHandlingAsync() 호출
   ↓
5. 실행 흐름:
   ① _onBeforeExecute() 호출 (Grain의 OnBeforeExecuteAsync)
   ② _executeWork() 호출 (Grain의 ExecuteWorkAsync)
   ③ 성공 시:
      - _successCount++
      - _lastExecutionTime 업데이트
      - _onAfterExecute() 호출
   ④ 실패 시:
      - _failureCount++
      - _onError(exception) 호출
```

## 💻 실전 사용 예제

### 1. StatelessWorkerGrainBase 사용

```csharp
// 데이터 정리 워커
public interface ICleanupWorkerGrain : IGrainWithStringKey
{
    Task StartAsync();
    Task StopAsync();
    Task<WorkerStatus> GetStatusAsync();
}

public class CleanupWorkerGrain : StatelessWorkerGrainBase, ICleanupWorkerGrain
{
    private readonly IClusterClient _client;

    public CleanupWorkerGrain(
        ILogger<CleanupWorkerGrain> logger,
        IClusterClient client)
        : base(logger)  // WorkerExecutor가 내부적으로 생성됨
    {
        _client = client;
    }

    // 워커 시작
    public Task StartAsync()
    {
        // 5초 후 첫 실행, 그 이후 1분마다 반복
        return StartTimerAsync(
            dueTime: TimeSpan.FromSeconds(5),
            period: TimeSpan.FromMinutes(1));
    }

    // 워커 중지
    public new Task StopAsync() => base.StopAsync();

    // WorkerExecutor가 주기적으로 호출할 메서드
    protected override async Task ExecuteWorkAsync()
    {
        Logger.LogInformation("Starting cleanup task");

        // 만료된 세션 정리
        var expiredSessions = await FindExpiredSessionsAsync();
        foreach (var sessionId in expiredSessions)
        {
            var session = _client.GetGrain<ISessionGrain>(sessionId);
            await session.DeleteAsync();
        }

        Logger.LogInformation("Cleanup completed. Removed {Count} sessions", expiredSessions.Count);
    }

    // 선택적: 작업 전 전처리
    protected override Task OnBeforeExecuteAsync()
    {
        Logger.LogDebug("Preparing cleanup task");
        return Task.CompletedTask;
    }

    // 선택적: 작업 후 후처리
    protected override Task OnAfterExecuteAsync()
    {
        Logger.LogDebug("Cleanup task finished");
        return Task.CompletedTask;
    }

    // 선택적: 에러 처리 커스터마이징
    protected override Task OnErrorAsync(Exception exception)
    {
        if (exception is TimeoutException)
        {
            Logger.LogWarning("Cleanup task timed out, will retry next cycle");
        }
        else
        {
            Logger.LogError(exception, "Cleanup task failed");
        }
        return Task.CompletedTask;
    }

    private Task<List<string>> FindExpiredSessionsAsync()
    {
        // 구현...
        return Task.FromResult(new List<string>());
    }
}
```

### 2. StatefulWorkerGrainBase 사용

```csharp
// 랭킹 집계 워커
public class RankingState
{
    public DateTime LastAggregationTime { get; set; }
    public int TotalProcessedPlayers { get; set; }
}

public interface IRankingAggregatorGrain : IGrainWithStringKey
{
    Task StartAsync();
    Task StopAsync();
    Task<WorkerStatus> GetStatusAsync();
    Task<RankingState> GetStateAsync();
}

public class RankingAggregatorGrain
    : StatefulWorkerGrainBase<RankingState>, IRankingAggregatorGrain
{
    private readonly IClusterClient _client;

    public RankingAggregatorGrain(
        [PersistentState("state")] IPersistentState<RankingState> state,
        ILogger<RankingAggregatorGrain> logger,
        IClusterClient client)
        : base(state, logger)  // WorkerExecutor가 내부적으로 생성됨
    {
        _client = client;
    }

    public Task StartAsync()
    {
        // 즉시 시작, 10분마다 반복
        return StartTimerAsync(
            dueTime: TimeSpan.Zero,
            period: TimeSpan.FromMinutes(10));
    }

    public new Task StopAsync() => base.StopAsync();

    public Task<RankingState> GetStateAsync()
    {
        return Task.FromResult(State);
    }

    // WorkerExecutor가 주기적으로 호출할 메서드
    protected override async Task ExecuteWorkAsync()
    {
        Logger.LogInformation("Starting ranking aggregation");

        var players = await GetAllPlayersAsync();
        var processedCount = 0;

        foreach (var playerId in players)
        {
            var player = _client.GetGrain<IPlayerGrain>(playerId);
            var stats = await player.GetStatsAsync();

            await UpdateRankingAsync(playerId, stats.Score);
            processedCount++;
        }

        // 상태 업데이트 및 자동 저장
        await UpdateStateAsync(state =>
        {
            state.LastAggregationTime = DateTime.UtcNow;
            state.TotalProcessedPlayers += processedCount;
        });

        Logger.LogInformation("Ranking aggregation completed. Processed {Count} players", processedCount);
    }

    protected override Task OnBeforeExecuteAsync()
    {
        Logger.LogInformation("Starting aggregation at {Time}", DateTime.UtcNow);
        return Task.CompletedTask;
    }

    protected override async Task OnAfterExecuteAsync()
    {
        // 통계 기록
        var status = await GetStatusAsync();
        Logger.LogInformation(
            "Aggregation stats - Success: {Success}, Failure: {Failure}",
            status.SuccessCount,
            status.FailureCount);
    }

    protected override async Task OnErrorAsync(Exception exception)
    {
        Logger.LogError(exception, "Ranking aggregation failed");

        // 실패 상태 기록
        await UpdateStateAsync(state =>
        {
            state.LastAggregationTime = DateTime.UtcNow;
        });
    }

    private Task<List<string>> GetAllPlayersAsync()
    {
        // 구현...
        return Task.FromResult(new List<string>());
    }

    private Task UpdateRankingAsync(string playerId, int score)
    {
        // 구현...
        return Task.CompletedTask;
    }
}
```

### 3. 워커 상태 모니터링

```csharp
// API 컨트롤러에서 워커 상태 확인
[ApiController]
[Route("api/workers")]
public class WorkerController : ControllerBase
{
    private readonly IClusterClient _client;

    public WorkerController(IClusterClient client)
    {
        _client = client;
    }

    [HttpGet("cleanup/status")]
    public async Task<IActionResult> GetCleanupStatus()
    {
        var worker = _client.GetGrain<ICleanupWorkerGrain>("singleton");
        var status = await worker.GetStatusAsync();

        return Ok(new
        {
            IsRunning = status.IsRunning,
            LastExecution = status.LastExecutionTime,
            NextExecution = status.NextExecutionTime,
            SuccessCount = status.SuccessCount,
            FailureCount = status.FailureCount,
            SuccessRate = status.SuccessCount + status.FailureCount > 0
                ? (double)status.SuccessCount / (status.SuccessCount + status.FailureCount) * 100
                : 0
        });
    }

    [HttpPost("cleanup/start")]
    public async Task<IActionResult> StartCleanup()
    {
        var worker = _client.GetGrain<ICleanupWorkerGrain>("singleton");
        await worker.StartAsync();
        return Ok("Cleanup worker started");
    }

    [HttpPost("cleanup/stop")]
    public async Task<IActionResult> StopCleanup()
    {
        var worker = _client.GetGrain<ICleanupWorkerGrain>("singleton");
        await worker.StopAsync();
        return Ok("Cleanup worker stopped");
    }

    [HttpPost("cleanup/reset-stats")]
    public async Task<IActionResult> ResetCleanupStats()
    {
        var worker = _client.GetGrain<ICleanupWorkerGrain>("singleton");
        await worker.ResetStatisticsAsync();
        return Ok("Statistics reset");
    }
}
```

## 🔧 WorkerExecutor의 핵심 기능

### 1. Timer 관리

```csharp
// WorkerExecutor.cs (109-136번 라인)
public Task StartTimerAsync(Grain grain, TimeSpan dueTime, TimeSpan period)
{
    if (_isRunning)
    {
        _logger.LogWarning("Worker grain {GrainId} is already running", _getGrainId());
        return Task.CompletedTask;
    }

    // Grain의 RegisterGrainTimer 사용
    _timer = grain.RegisterGrainTimer(
        async _ => await ExecuteWorkWithErrorHandlingAsync(),
        new GrainTimerCreationOptions
        {
            DueTime = dueTime,
            Period = period,
            Interleave = true  // 동시 실행 허용
        });

    _isRunning = true;
    lock (_statsLock)
    {
        _nextExecutionTime = DateTime.UtcNow.Add(dueTime);
    }

    _logger.LogInformation(
        "Worker grain {GrainId} started with timer (DueTime: {DueTime}, Period: {Period})",
        _getGrainId(), dueTime, period);

    return Task.CompletedTask;
}
```

### 2. 에러 처리 및 통계 수집

```csharp
// WorkerExecutor.cs (207-237번 라인)
private async Task ExecuteWorkWithErrorHandlingAsync()
{
    var startTime = DateTime.UtcNow;

    try
    {
        // 1. 전처리 훅 실행
        await _onBeforeExecute();

        // 2. 실제 작업 실행
        _logger.LogDebug("Executing work for worker grain {GrainId}", _getGrainId());
        await _executeWork();

        // 3. 성공 통계 업데이트 (Thread-safe)
        lock (_statsLock)
        {
            _successCount++;
            _lastExecutionTime = startTime;
        }

        // 4. 후처리 훅 실행
        await _onAfterExecute();

        _logger.LogDebug("Work executed successfully for worker grain {GrainId}", _getGrainId());
    }
    catch (Exception ex)
    {
        // 5. 실패 통계 업데이트
        lock (_statsLock)
        {
            _failureCount++;
        }

        // 6. 에러 처리 훅 실행
        await _onError(ex);
    }
}
```

### 3. 스레드 안전한 통계 관리

```csharp
// 모든 통계 필드는 lock으로 보호됨
private readonly object _statsLock = new();

public int SuccessCount
{
    get
    {
        lock (_statsLock)
        {
            return _successCount;
        }
    }
}

public WorkerStatus GetStatus()
{
    lock (_statsLock)
    {
        return new WorkerStatus
        {
            IsRunning = _isRunning,
            LastExecutionTime = _lastExecutionTime,
            NextExecutionTime = _nextExecutionTime,
            SuccessCount = _successCount,
            FailureCount = _failureCount
        };
    }
}
```

## 📊 디자인 패턴 분석

### 1. 전략 패턴 (Strategy Pattern)

```csharp
// WorkerExecutor는 실행 방법을 알지만, 실제 '무엇을' 실행할지는 모름
// Grain이 전략(Func<Task>)을 주입함

// 전략 정의
private readonly Func<Task> _executeWork;         // 전략 1: 작업 실행
private readonly Func<Task> _onBeforeExecute;     // 전략 2: 전처리
private readonly Func<Exception, Task> _onError;  // 전략 3: 에러 처리

// Grain에서 전략 제공
_executor = new WorkerExecutor(
    logger,
    ExecuteWorkAsync,        // ← Grain이 제공하는 전략
    OnBeforeExecuteAsync,    // ← Grain이 제공하는 전략
    OnAfterExecuteAsync,     // ← Grain이 제공하는 전략
    OnErrorAsync,            // ← Grain이 제공하는 전략
    () => this.GetPrimaryKeyString());
```

### 2. 템플릿 메서드 패턴 (Template Method Pattern)

```csharp
// ExecuteWorkWithErrorHandlingAsync가 템플릿
private async Task ExecuteWorkWithErrorHandlingAsync()
{
    // 템플릿 구조 (절차는 고정):
    // 1. 전처리
    // 2. 작업 실행
    // 3. 성공 처리 또는 에러 처리
    // 4. 후처리

    // 하지만 각 단계의 구체적인 행동은 주입된 전략에 위임
    await _onBeforeExecute();   // Grain이 정의
    await _executeWork();       // Grain이 정의
    await _onAfterExecute();    // Grain이 정의
}
```

### 3. 위임 패턴 (Delegation Pattern)

```csharp
// Grain 베이스 클래스는 WorkerExecutor에 모든 작업 위임
public abstract class StatelessWorkerGrainBase : Grain
{
    private readonly WorkerExecutor _executor;

    // 모든 메서드가 단순 위임
    protected virtual Task StartTimerAsync(TimeSpan dueTime, TimeSpan period)
        => _executor.StartTimerAsync(this, dueTime, period);

    protected virtual Task StopAsync()
        => _executor.StopAsync();

    public virtual Task<WorkerStatus> GetStatusAsync()
        => Task.FromResult(_executor.GetStatus());

    public virtual Task ResetStatisticsAsync()
    {
        _executor.ResetStatistics();
        return Task.CompletedTask;
    }
}
```

## ⚠️ 주의사항

### 1. WorkerExecutor는 Internal 클래스

```csharp
// OrleansX.Grains/Internal/WorkerExecutor.cs
internal class WorkerExecutor  // ← internal: 외부에 노출되지 않음
{
    // 사용자는 WorkerExecutor를 직접 사용할 수 없음
    // 반드시 StatelessWorkerGrainBase 또는 StatefulWorkerGrainBase를 상속
}
```

**이유:**
- 구현 세부사항을 숨김 (Encapsulation)
- 사용자는 안정적인 베이스 클래스 API만 사용
- 내부 구현 변경이 사용자 코드에 영향을 주지 않음

### 2. Thread Safety

```csharp
// ✅ 통계 필드는 lock으로 보호됨
private readonly object _statsLock = new();

lock (_statsLock)
{
    _successCount++;
    _lastExecutionTime = startTime;
}

// ❌ 만약 lock이 없다면?
// 여러 스레드에서 동시에 _successCount++를 실행하면
// Lost Update 문제 발생 가능
```

### 3. Timer vs Reminder

```csharp
// WorkerExecutor는 Timer만 지원
// Reminder가 필요하면 IRemindable을 직접 구현해야 함

public class MyWorkerGrain : StatelessWorkerGrainBase, IRemindable
{
    public async Task ReceiveReminder(string reminderName, TickStatus status)
    {
        // Reminder 콜백 처리
        await ExecuteWorkAsync();
    }

    public Task StartWithReminderAsync()
    {
        return this.RegisterOrUpdateReminder(
            "MyReminder",
            TimeSpan.FromMinutes(1),
            TimeSpan.FromMinutes(10));
    }
}
```

**Timer vs Reminder 비교:**

| 특징 | Timer | Reminder |
|------|-------|----------|
| **영속성** | ❌ Grain 비활성화 시 사라짐 | ✅ 영구적으로 유지 |
| **복구** | ❌ 재시작 후 재등록 필요 | ✅ 자동 복구 |
| **성능** | ✅ 가볍고 빠름 | ⚠️ Storage 오버헤드 |
| **사용 사례** | 캐시 정리, 임시 작업 | 일정 알림, 정기 청구 |

## 🔗 관련 문서

- [Worker Grains](../Grains/WorkerGrains.md) - Worker Grain 사용 가이드
- [의존성 주입](DependencyInjection.md) - DI 패턴 이해
- [Orleans 기초](../Orleans-Basics.md) - Grain 생명주기, Timer, Reminder

## 📚 참고 자료

- [Orleans Timers](https://learn.microsoft.com/dotnet/orleans/grains/timers-and-reminders)
- [Orleans Reminders](https://learn.microsoft.com/dotnet/orleans/grains/timers-and-reminders#reminders)
- [전략 패턴](https://refactoring.guru/design-patterns/strategy)
- [템플릿 메서드 패턴](https://refactoring.guru/design-patterns/template-method)
