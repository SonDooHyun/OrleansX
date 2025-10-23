namespace OrleansX.Abstractions.Options;

/// <summary>
/// Orleans Client 설정 옵션
/// </summary>
public class OrleansClientOptions
{
    /// <summary>
    /// 클러스터 ID
    /// </summary>
    public required string ClusterId { get; init; }

    /// <summary>
    /// 서비스 ID
    /// </summary>
    public required string ServiceId { get; init; }

    /// <summary>
    /// 데이터베이스 설정
    /// </summary>
    public DatabaseOptions? Db { get; init; }

    /// <summary>
    /// 재시도 설정
    /// </summary>
    public RetryOptions? Retry { get; init; }

    /// <summary>
    /// 연결 재시도 설정
    /// </summary>
    public ConnectionRetryOptions? ConnectionRetry { get; init; }
}

/// <summary>
/// 데이터베이스 설정
/// </summary>
public record DatabaseOptions(string DbInvariant, string ConnectionString);

/// <summary>
/// 재시도 설정
/// </summary>
public class RetryOptions
{
    /// <summary>
    /// 최대 재시도 횟수
    /// </summary>
    public int MaxAttempts { get; init; } = 3;

    /// <summary>
    /// 기본 지연 시간 (밀리초)
    /// </summary>
    public int BaseDelayMs { get; init; } = 200;

    /// <summary>
    /// 최대 지연 시간 (밀리초)
    /// </summary>
    public int MaxDelayMs { get; init; } = 10000;
}

/// <summary>
/// 연결 재시도 설정
/// </summary>
public class ConnectionRetryOptions
{
    /// <summary>
    /// 재연결 시도 간격 (초)
    /// </summary>
    public int RetryIntervalSeconds { get; init; } = 5;

    /// <summary>
    /// 최대 재연결 시도 횟수 (0은 무제한)
    /// </summary>
    public int MaxRetryAttempts { get; init; } = 0;
}



