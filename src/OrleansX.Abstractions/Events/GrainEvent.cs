using Orleans;

namespace OrleansX.Abstractions.Events;

/// <summary>
/// Grain 이벤트 베이스 클래스
/// </summary>
[GenerateSerializer]
public abstract class GrainEvent
{
    /// <summary>
    /// 이벤트 ID
    /// </summary>
    [Id(0)]
    public string EventId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// 이벤트 발생 시간
    /// </summary>
    [Id(1)]
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// 이벤트 타입
    /// </summary>
    [Id(2)]
    public string EventType { get; set; } = string.Empty;

    /// <summary>
    /// 상관관계 ID
    /// </summary>
    [Id(3)]
    public string? CorrelationId { get; set; }
}

