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
    /// 파티 리더 ID
    /// </summary>
    [Id(1)]
    public string LeaderId { get; set; } = string.Empty;

    /// <summary>
    /// 파티 멤버 목록
    /// </summary>
    [Id(2)]
    public List<PartyMember> Members { get; set; } = new();

    /// <summary>
    /// 최대 멤버 수
    /// </summary>
    [Id(3)]
    public int MaxMembers { get; set; } = 4;

    /// <summary>
    /// 파티 상태
    /// </summary>
    [Id(4)]
    public PartyStatus Status { get; set; } = PartyStatus.Waiting;

    /// <summary>
    /// 매칭 대기 중 여부
    /// </summary>
    [Id(5)]
    public bool IsMatchmaking { get; set; }

    /// <summary>
    /// 생성 시간
    /// </summary>
    [Id(6)]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// 마지막 업데이트 시간
    /// </summary>
    [Id(7)]
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
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
    public DateTimeOffset JoinedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// 파티 상태
/// </summary>
public enum PartyStatus
{
    Waiting,     // 대기 중
    InGame,      // 게임 중
    Disbanded    // 해산됨
}

