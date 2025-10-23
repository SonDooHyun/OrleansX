namespace OrleansX.Abstractions;

/// <summary>
/// 재시도 정책 인터페이스
/// </summary>
public interface IRetryPolicy
{
    /// <summary>
    /// 재시도 가능한 예외인지 확인합니다.
    /// </summary>
    bool ShouldRetry(Exception exception, int attemptNumber);

    /// <summary>
    /// 다음 재시도까지의 지연 시간을 계산합니다.
    /// </summary>
    TimeSpan GetNextDelay(int attemptNumber);
}



