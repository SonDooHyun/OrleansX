using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Runtime;
using Orleans.Timers;

namespace OrleansX.Grains;

/// <summary>
/// 상태를 가진 워커 Grain의 베이스 클래스
/// 백그라운드 작업, 스케줄링된 작업 등에 사용됩니다.
/// </summary>
/// <typeparam name="TState">Grain의 상태 타입</typeparam>
public abstract class StatefulWorkerGrainBase<TState> : Grain where TState : class, new()
{
    private readonly IPersistentState<TState> _state;
    protected readonly ILogger Logger;
    private IDisposable? _timer;
    private bool _isRunning;
    private DateTime? _lastExecutionTime;
    private DateTime? _nextExecutionTime;
    private int _successCount;
    private int _failureCount;
    private readonly object _statsLock = new();

    protected StatefulWorkerGrainBase(
        [PersistentState("state")] IPersistentState<TState> state,
        ILogger logger)
    {
        _state = state ?? throw new ArgumentNullException(nameof(state));
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Grain의 현재 상태
    /// </summary>
    protected TState State => _state.State;

    /// <summary>
    /// 상태가 로드되었는지 여부
    /// </summary>
    protected bool IsStateRecorded => _state.RecordExists;

    /// <summary>
    /// 상태의 ETag
    /// </summary>
    protected string? StateEtag => _state.Etag;

    /// <summary>
    /// 워커가 실행 중인지 여부
    /// </summary>
    protected bool IsRunning => _isRunning;

    /// <summary>
    /// 마지막 작업 실행 시간
    /// </summary>
    protected DateTime? LastExecutionTime
    {
        get
        {
            lock (_statsLock)
            {
                return _lastExecutionTime;
            }
        }
    }

    /// <summary>
    /// 다음 작업 실행 예정 시간
    /// </summary>
    protected DateTime? NextExecutionTime
    {
        get
        {
            lock (_statsLock)
            {
                return _nextExecutionTime;
            }
        }
    }

    /// <summary>
    /// 성공한 작업 실행 횟수
    /// </summary>
    protected int SuccessCount
    {
        get
        {
            lock (_statsLock)
            {
                return _successCount;
            }
        }
    }

    /// <summary>
    /// 실패한 작업 실행 횟수
    /// </summary>
    protected int FailureCount
    {
        get
        {
            lock (_statsLock)
            {
                return _failureCount;
            }
        }
    }

    /// <summary>
    /// 상태를 저장합니다.
    /// </summary>
    protected virtual async Task SaveStateAsync()
    {
        try
        {
            await _state.WriteStateAsync();
            Logger.LogDebug("State saved successfully for worker grain {GrainId}", this.GetPrimaryKeyString());
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to save state for worker grain {GrainId}", this.GetPrimaryKeyString());
            throw;
        }
    }

    /// <summary>
    /// 상태를 읽습니다.
    /// </summary>
    protected virtual async Task ReadStateAsync()
    {
        try
        {
            await _state.ReadStateAsync();
            Logger.LogDebug("State read successfully for worker grain {GrainId}", this.GetPrimaryKeyString());
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to read state for worker grain {GrainId}", this.GetPrimaryKeyString());
            throw;
        }
    }

    /// <summary>
    /// 상태를 초기화합니다.
    /// </summary>
    protected virtual async Task ClearStateAsync()
    {
        try
        {
            await _state.ClearStateAsync();
            Logger.LogDebug("State cleared successfully for worker grain {GrainId}", this.GetPrimaryKeyString());
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to clear state for worker grain {GrainId}", this.GetPrimaryKeyString());
            throw;
        }
    }

    /// <summary>
    /// 상태를 업데이트하고 저장합니다.
    /// </summary>
    protected virtual async Task UpdateStateAsync(Action<TState> updateAction)
    {
        if (updateAction == null)
            throw new ArgumentNullException(nameof(updateAction));

        updateAction(State);
        await SaveStateAsync();
    }

    /// <summary>
    /// Timer를 사용하여 주기적인 작업을 시작합니다.
    /// </summary>
    /// <param name="dueTime">첫 실행까지의 지연 시간</param>
    /// <param name="period">실행 주기</param>
    protected virtual Task StartTimerAsync(TimeSpan dueTime, TimeSpan period)
    {
        if (_isRunning)
        {
            Logger.LogWarning("Worker grain {GrainId} is already running", this.GetPrimaryKeyString());
            return Task.CompletedTask;
        }

        _timer = this.RegisterGrainTimer(
            async _ => await ExecuteWorkWithErrorHandlingAsync(),
            new GrainTimerCreationOptions
            {
                DueTime = dueTime,
                Period = period,
                Interleave = true
            });

        _isRunning = true;
        lock (_statsLock)
        {
            _nextExecutionTime = DateTime.UtcNow.Add(dueTime);
        }

        Logger.LogInformation("Worker grain {GrainId} started with timer (DueTime: {DueTime}, Period: {Period})", 
            this.GetPrimaryKeyString(), dueTime, period);

        return Task.CompletedTask;
    }

    // Reminder 기능은 사용자가 필요 시 IRemindable 인터페이스를 직접 구현하여 사용하세요.
    // Orleans 9.x에서는 Reminder를 사용하기 위해 명시적으로 IRemindable을 구현해야 합니다.

    /// <summary>
    /// 작업을 중지합니다.
    /// </summary>
    protected virtual async Task StopAsync()
    {
        if (!_isRunning)
        {
            Logger.LogWarning("Worker grain {GrainId} is not running", this.GetPrimaryKeyString());
            return;
        }

        _timer?.Dispose();
        _timer = null;

        _isRunning = false;
        lock (_statsLock)
        {
            _nextExecutionTime = null;
        }

        Logger.LogInformation("Worker grain {GrainId} stopped", this.GetPrimaryKeyString());

        await Task.CompletedTask;
    }

    /// <summary>
    /// 실행할 작업을 정의합니다. (파생 클래스에서 구현 필요)
    /// </summary>
    protected abstract Task ExecuteWorkAsync();

    /// <summary>
    /// 작업 실행 전 호출되는 훅입니다. (선택적 오버라이드)
    /// </summary>
    protected virtual Task OnBeforeExecuteAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// 작업 실행 후 호출되는 훅입니다. (선택적 오버라이드)
    /// </summary>
    protected virtual Task OnAfterExecuteAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// 작업 실행 중 에러 발생 시 호출되는 훅입니다. (선택적 오버라이드)
    /// </summary>
    /// <param name="exception">발생한 예외</param>
    protected virtual Task OnErrorAsync(Exception exception)
    {
        Logger.LogError(exception, "Error occurred during work execution in worker grain {GrainId}", 
            this.GetPrimaryKeyString());
        return Task.CompletedTask;
    }

    /// <summary>
    /// 에러 처리를 포함한 작업 실행 래퍼
    /// </summary>
    private async Task ExecuteWorkWithErrorHandlingAsync()
    {
        var startTime = DateTime.UtcNow;

        try
        {
            await OnBeforeExecuteAsync();

            Logger.LogDebug("Executing work for worker grain {GrainId}", this.GetPrimaryKeyString());
            await ExecuteWorkAsync();

            lock (_statsLock)
            {
                _successCount++;
                _lastExecutionTime = startTime;
            }

            await OnAfterExecuteAsync();

            Logger.LogDebug("Work executed successfully for worker grain {GrainId}", this.GetPrimaryKeyString());
        }
        catch (Exception ex)
        {
            lock (_statsLock)
            {
                _failureCount++;
            }

            await OnErrorAsync(ex);
        }
    }


    /// <summary>
    /// Grain이 활성화될 때 호출됩니다.
    /// </summary>
    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        Logger.LogInformation("Worker grain {GrainId} activated", this.GetPrimaryKeyString());
        return base.OnActivateAsync(cancellationToken);
    }

    /// <summary>
    /// Grain이 비활성화될 때 호출됩니다.
    /// </summary>
    public override async Task OnDeactivateAsync(DeactivationReason reason, CancellationToken cancellationToken)
    {
        Logger.LogInformation("Worker grain {GrainId} deactivating. Reason: {Reason}", 
            this.GetPrimaryKeyString(), reason);

        // Timer 정리 (Reminder는 자동으로 유지됨)
        _timer?.Dispose();

        await base.OnDeactivateAsync(reason, cancellationToken);
    }

    /// <summary>
    /// 워커 상태 정보를 조회합니다.
    /// </summary>
    public virtual Task<WorkerStatus> GetStatusAsync()
    {
        lock (_statsLock)
        {
            return Task.FromResult(new WorkerStatus
            {
                IsRunning = _isRunning,
                LastExecutionTime = _lastExecutionTime,
                NextExecutionTime = _nextExecutionTime,
                SuccessCount = _successCount,
                FailureCount = _failureCount
            });
        }
    }

    /// <summary>
    /// 워커 통계를 초기화합니다.
    /// </summary>
    public virtual Task ResetStatisticsAsync()
    {
        lock (_statsLock)
        {
            _successCount = 0;
            _failureCount = 0;
            _lastExecutionTime = null;
        }

        Logger.LogInformation("Statistics reset for worker grain {GrainId}", this.GetPrimaryKeyString());
        return Task.CompletedTask;
    }
}

/// <summary>
/// 워커 상태 정보
/// </summary>
[GenerateSerializer]
public class WorkerStatus
{
    /// <summary>
    /// 실행 중 여부
    /// </summary>
    [Id(0)]
    public bool IsRunning { get; set; }

    /// <summary>
    /// 마지막 실행 시간
    /// </summary>
    [Id(1)]
    public DateTime? LastExecutionTime { get; set; }

    /// <summary>
    /// 다음 실행 예정 시간
    /// </summary>
    [Id(2)]
    public DateTime? NextExecutionTime { get; set; }

    /// <summary>
    /// 성공 횟수
    /// </summary>
    [Id(3)]
    public int SuccessCount { get; set; }

    /// <summary>
    /// 실패 횟수
    /// </summary>
    [Id(4)]
    public int FailureCount { get; set; }
}

