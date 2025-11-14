using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Runtime;
using Orleans.Timers;

namespace OrleansX.Grains.Internal;

/// <summary>
/// Worker Grain의 공통 로직을 캡슐화하는 내부 헬퍼 클래스
/// 상속 계층의 복잡도를 증가시키지 않으면서 코드 중복을 제거합니다.
/// </summary>
internal class WorkerExecutor
{
    private readonly ILogger _logger;
    private readonly Func<Task> _executeWork;
    private readonly Func<Task> _onBeforeExecute;
    private readonly Func<Exception, Task> _onError;
    private readonly Func<Task> _onAfterExecute;
    private readonly Func<string> _getGrainId;

    private IDisposable? _timer;
    private bool _isRunning;
    private DateTime? _lastExecutionTime;
    private DateTime? _nextExecutionTime;
    private int _successCount;
    private int _failureCount;
    private readonly object _statsLock = new();

    public WorkerExecutor(
        ILogger logger,
        Func<Task> executeWork,
        Func<Task> onBeforeExecute,
        Func<Task> onAfterExecute,
        Func<Exception, Task> onError,
        Func<string> getGrainId)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _executeWork = executeWork ?? throw new ArgumentNullException(nameof(executeWork));
        _onBeforeExecute = onBeforeExecute ?? throw new ArgumentNullException(nameof(onBeforeExecute));
        _onAfterExecute = onAfterExecute ?? throw new ArgumentNullException(nameof(onAfterExecute));
        _onError = onError ?? throw new ArgumentNullException(nameof(onError));
        _getGrainId = getGrainId ?? throw new ArgumentNullException(nameof(getGrainId));
    }

    /// <summary>
    /// 워커가 실행 중인지 여부
    /// </summary>
    public bool IsRunning => _isRunning;

    /// <summary>
    /// 마지막 작업 실행 시간
    /// </summary>
    public DateTime? LastExecutionTime
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
    public DateTime? NextExecutionTime
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

    /// <summary>
    /// 실패한 작업 실행 횟수
    /// </summary>
    public int FailureCount
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
    /// Timer를 사용하여 주기적인 작업을 시작합니다.
    /// </summary>
    public Task StartTimerAsync(Grain grain, TimeSpan dueTime, TimeSpan period)
    {
        if (_isRunning)
        {
            _logger.LogWarning("Worker grain {GrainId} is already running", _getGrainId());
            return Task.CompletedTask;
        }

        _timer = grain.RegisterGrainTimer(
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

        _logger.LogInformation("Worker grain {GrainId} started with timer (DueTime: {DueTime}, Period: {Period})",
            _getGrainId(), dueTime, period);

        return Task.CompletedTask;
    }

    /// <summary>
    /// 작업을 중지합니다.
    /// </summary>
    public Task StopAsync()
    {
        if (!_isRunning)
        {
            _logger.LogWarning("Worker grain {GrainId} is not running", _getGrainId());
            return Task.CompletedTask;
        }

        _timer?.Dispose();
        _timer = null;

        _isRunning = false;
        lock (_statsLock)
        {
            _nextExecutionTime = null;
        }

        _logger.LogInformation("Worker grain {GrainId} stopped", _getGrainId());

        return Task.CompletedTask;
    }

    /// <summary>
    /// Timer 리소스를 정리합니다.
    /// </summary>
    public void Dispose()
    {
        _timer?.Dispose();
    }

    /// <summary>
    /// 워커 상태 정보를 조회합니다.
    /// </summary>
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

    /// <summary>
    /// 워커 통계를 초기화합니다.
    /// </summary>
    public void ResetStatistics()
    {
        lock (_statsLock)
        {
            _successCount = 0;
            _failureCount = 0;
            _lastExecutionTime = null;
        }

        _logger.LogInformation("Statistics reset for worker grain {GrainId}", _getGrainId());
    }

    /// <summary>
    /// 에러 처리를 포함한 작업 실행 래퍼
    /// </summary>
    private async Task ExecuteWorkWithErrorHandlingAsync()
    {
        var startTime = DateTime.UtcNow;

        try
        {
            await _onBeforeExecute();

            _logger.LogDebug("Executing work for worker grain {GrainId}", _getGrainId());
            await _executeWork();

            lock (_statsLock)
            {
                _successCount++;
                _lastExecutionTime = startTime;
            }

            await _onAfterExecute();

            _logger.LogDebug("Work executed successfully for worker grain {GrainId}", _getGrainId());
        }
        catch (Exception ex)
        {
            lock (_statsLock)
            {
                _failureCount++;
            }

            await _onError(ex);
        }
    }
}
