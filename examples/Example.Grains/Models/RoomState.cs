using Orleans;

namespace Example.Grains.Models;

/// <summary>
/// 게임 룸 상태
/// </summary>
[GenerateSerializer]
public class RoomState
{
    /// <summary>
    /// 룸 ID
    /// </summary>
    [Id(0)]
    public string RoomId { get; set; } = string.Empty;

    /// <summary>
    /// 팀 A
    /// </summary>
    [Id(1)]
    public List<RoomPlayer> TeamA { get; set; } = new();

    /// <summary>
    /// 팀 B
    /// </summary>
    [Id(2)]
    public List<RoomPlayer> TeamB { get; set; } = new();

    /// <summary>
    /// 룸 상태
    /// </summary>
    [Id(3)]
    public RoomStatus Status { get; set; } = RoomStatus.CharacterSelection;

    /// <summary>
    /// 매치 정보
    /// </summary>
    [Id(4)]
    public MatchInfo? MatchInfo { get; set; }

    /// <summary>
    /// 생성일
    /// </summary>
    [Id(5)]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 게임 시작 시간
    /// </summary>
    [Id(6)]
    public DateTime? GameStartedAt { get; set; }
}

/// <summary>
/// 룸 플레이어
/// </summary>
[GenerateSerializer]
public class RoomPlayer
{
    [Id(0)]
    public string PlayerId { get; set; } = string.Empty;

    [Id(1)]
    public string PlayerName { get; set; } = string.Empty;

    [Id(2)]
    public int Mmr { get; set; }

    [Id(3)]
    public string? PartyId { get; set; }

    [Id(4)]
    public string? SelectedCharacterId { get; set; }

    [Id(5)]
    public bool IsReady { get; set; }

    [Id(6)]
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// 룸 상태
/// </summary>
[GenerateSerializer]
public enum RoomStatus
{
    [Id(0)] CharacterSelection,  // 캐릭터 선택 중
    [Id(1)] Ready,               // 준비 완료
    [Id(2)] InGame,              // 게임 진행 중
    [Id(3)] Finished             // 게임 종료
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
    public int AverageMmr { get; set; }

    [Id(2)]
    public DateTime MatchedAt { get; set; } = DateTime.UtcNow;
}

