namespace OrleansX.Abstractions;

/// <summary>
/// Idempotency Key 제공자 인터페이스
/// </summary>
public interface IIdempotencyKeyProvider
{
    /// <summary>
    /// 현재 컨텍스트의 Idempotency Key를 가져옵니다.
    /// </summary>
    string? GetIdempotencyKey();

    /// <summary>
    /// Idempotency Key를 설정합니다.
    /// </summary>
    void SetIdempotencyKey(string key);
}

