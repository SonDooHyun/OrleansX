using Orleans;

namespace Example.Grains.Models;

/// <summary>
/// 파티 상태
/// </summary>
[GenerateSerializer]
public class PartyState
{
    /// <summary>
    /// 파티 ID
    /// </summary>
    [Id(0)]
    public string PartyId { get; set; } = string.Empty;

    /// <summary>
    /// 리더 ID
    /// </summary>
    [Id(1)]
    public string LeaderId { get; set; } = string.Empty;

    /// <summary>
    /// 멤버 목록
    /// </summary>
    [Id(2)]
    public List<PartyMember> Members { get; set; } = new();

    /// <summary>
    /// 최대 인원
    /// </summary>
    [Id(3)]
    public int MaxMembers { get; set; } = 5;

    /// <summary>
    /// 파티 상태
    /// </summary>
    [Id(4)]
    public PartyStatus Status { get; set; } = PartyStatus.Waiting;

    /// <summary>
    /// 매칭 중 여부
    /// </summary>
    [Id(5)]
    public bool IsMatchmaking { get; set; }

    /// <summary>
    /// 평균 MMR
    /// </summary>
    [Id(6)]
    public int AverageMmr { get; set; }

    /// <summary>
    /// 생성일
    /// </summary>
    [Id(7)]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// 파티 멤버
/// </summary>
[GenerateSerializer]
public class PartyMember
{
    [Id(0)]
    public string PlayerId { get; set; } = string.Empty;

    [Id(1)]
    public string PlayerName { get; set; } = string.Empty;

    [Id(2)]
    public int Level { get; set; }

    [Id(3)]
    public int Mmr { get; set; }

    [Id(4)]
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// 파티 상태
/// </summary>
[GenerateSerializer]
public enum PartyStatus
{
    [Id(0)] Waiting,        // 대기 중
    [Id(1)] Matchmaking,    // 매칭 중
    [Id(2)] InRoom,         // 룸에 입장
    [Id(3)] Playing,        // 게임 중
    [Id(4)] Disbanded       // 해산됨
}

