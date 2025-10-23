using OrleansX.Abstractions;

namespace OrleansX.Client.Idempotency;

/// <summary>
/// AsyncLocal 기반 Idempotency Key 제공자
/// </summary>
public class AsyncLocalIdempotencyKeyProvider : IIdempotencyKeyProvider
{
    private static readonly AsyncLocal<string?> _idempotencyKey = new();

    public string? GetIdempotencyKey()
    {
        return _idempotencyKey.Value;
    }

    public void SetIdempotencyKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Idempotency key cannot be null or empty", nameof(key));

        _idempotencyKey.Value = key;
    }
}



