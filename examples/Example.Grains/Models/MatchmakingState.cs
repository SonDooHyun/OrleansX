using Orleans;

namespace Example.Grains.Models;

/// <summary>
/// 매칭메이킹 상태
/// </summary>
[GenerateSerializer]
public class MatchmakingState
{
    /// <summary>
    /// 개인 매칭 큐 (PlayerId -> MatchRequest)
    /// </summary>
    [Id(0)]
    public Dictionary<string, MatchRequest> SoloQueue { get; set; } = new();

    /// <summary>
    /// 파티 매칭 큐 (PartyId -> MatchRequest)
    /// </summary>
    [Id(1)]
    public Dictionary<string, MatchRequest> PartyQueue { get; set; } = new();

    /// <summary>
    /// 생성된 매치 목록
    /// </summary>
    [Id(2)]
    public List<CreatedMatch> Matches { get; set; } = new();

    /// <summary>
    /// 마지막 매칭 시도 시간
    /// </summary>
    [Id(3)]
    public DateTime LastMatchAttempt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// 매칭 요청
/// </summary>
[GenerateSerializer]
public class MatchRequest
{
    [Id(0)]
    public string Id { get; set; } = string.Empty; // PlayerId or PartyId

    [Id(1)]
    public MatchType Type { get; set; }

    [Id(2)]
    public int AverageMmr { get; set; }

    [Id(3)]
    public int PlayerCount { get; set; }

    [Id(4)]
    public List<string> PlayerIds { get; set; } = new();

    [Id(5)]
    public DateTime EnqueuedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 대기 시간에 따른 MMR 범위 계산
    /// </summary>
    public int GetMmrRange()
    {
        var waitTime = DateTime.UtcNow - EnqueuedAt;
        var baseRange = 100;
        var expandedRange = (int)(waitTime.TotalSeconds / 10) * 50; // 10초마다 50 MMR 확장
        return Math.Min(baseRange + expandedRange, 500); // 최대 500
    }
}

/// <summary>
/// 매칭 타입
/// </summary>
[GenerateSerializer]
public enum MatchType
{
    [Id(0)] Solo,   // 개인 매칭
    [Id(1)] Party   // 파티 매칭
}

/// <summary>
/// 생성된 매치
/// </summary>
[GenerateSerializer]
public class CreatedMatch
{
    [Id(0)]
    public string MatchId { get; set; } = string.Empty;

    [Id(1)]
    public string RoomId { get; set; } = string.Empty;

    [Id(2)]
    public List<string> TeamA { get; set; } = new();

    [Id(3)]
    public List<string> TeamB { get; set; } = new();

    [Id(4)]
    public int AverageMmr { get; set; }

    [Id(5)]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

