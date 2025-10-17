using Orleans;

namespace Example.Grains.Models;

/// <summary>
/// 매칭 큐 상태
/// </summary>
[GenerateSerializer]
public class MatchmakingState
{
    /// <summary>
    /// 대기 중인 파티 목록 (PartyId, 레이팅)
    /// </summary>
    [Id(0)]
    public List<QueuedParty> QueuedParties { get; set; } = new();

    /// <summary>
    /// 활성 매치 목록
    /// </summary>
    [Id(1)]
    public Dictionary<string, MatchInfo> ActiveMatches { get; set; } = new();

    /// <summary>
    /// 마지막 매칭 시도 시간
    /// </summary>
    [Id(2)]
    public DateTimeOffset LastMatchAttempt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// 대기 중인 파티
/// </summary>
[GenerateSerializer]
public class QueuedParty
{
    [Id(0)]
    public string PartyId { get; set; } = string.Empty;

    [Id(1)]
    public int AverageRating { get; set; }

    [Id(2)]
    public int PartySize { get; set; }

    [Id(3)]
    public DateTimeOffset QueuedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// 매치 정보
/// </summary>
[GenerateSerializer]
public class MatchInfo
{
    [Id(0)]
    public string MatchId { get; set; } = string.Empty;

    [Id(1)]
    public List<string> PartyIds { get; set; } = new();

    [Id(2)]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    [Id(3)]
    public MatchStatus Status { get; set; } = MatchStatus.Preparing;
}

public enum MatchStatus
{
    Preparing,   // 준비 중
    InProgress,  // 진행 중
    Completed,   // 완료
    Cancelled    // 취소됨
}

