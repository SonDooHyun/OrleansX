using OrleansX.Abstractions;
using OrleansX.Abstractions.Exceptions;

namespace OrleansX.Client.Retry;

/// <summary>
/// 지수 백오프 재시도 정책
/// </summary>
public class ExponentialRetryPolicy : IRetryPolicy
{
    private readonly int _maxAttempts;
    private readonly int _baseDelayMs;
    private readonly int _maxDelayMs;

    public ExponentialRetryPolicy(int maxAttempts = 3, int baseDelayMs = 200, int maxDelayMs = 10000)
    {
        _maxAttempts = maxAttempts;
        _baseDelayMs = baseDelayMs;
        _maxDelayMs = maxDelayMs;
    }

    public bool ShouldRetry(Exception exception, int attemptNumber)
    {
        if (attemptNumber >= _maxAttempts)
            return false;

        // 재시도 가능한 예외 타입 확인
        return exception is RetryableException
            || exception is TimeoutException
            || exception is System.Net.Sockets.SocketException
            || exception is System.IO.IOException;
    }

    public TimeSpan GetNextDelay(int attemptNumber)
    {
        if (attemptNumber <= 0)
            return TimeSpan.Zero;

        var delayMs = Math.Min(
            _baseDelayMs * Math.Pow(2, attemptNumber - 1),
            _maxDelayMs
        );

        // 지터 추가 (랜덤하게 ±20%)
        var jitter = Random.Shared.NextDouble() * 0.4 - 0.2;
        delayMs *= (1 + jitter);

        return TimeSpan.FromMilliseconds(delayMs);
    }
}

