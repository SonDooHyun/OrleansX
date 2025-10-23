using Orleans;

namespace Example.Grains.Models;

/// <summary>
/// 플레이어 상태
/// </summary>
[GenerateSerializer]
public class PlayerState
{
    /// <summary>
    /// 플레이어 ID
    /// </summary>
    [Id(0)]
    public string PlayerId { get; set; } = string.Empty;

    /// <summary>
    /// 플레이어 이름
    /// </summary>
    [Id(1)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 레벨
    /// </summary>
    [Id(2)]
    public int Level { get; set; }

    /// <summary>
    /// MMR (Matchmaking Rating)
    /// </summary>
    [Id(3)]
    public int Mmr { get; set; } = 1000;

    /// <summary>
    /// 현재 파티 ID (파티에 속한 경우)
    /// </summary>
    [Id(4)]
    public string? CurrentPartyId { get; set; }

    /// <summary>
    /// 현재 룸 ID (게임 중인 경우)
    /// </summary>
    [Id(5)]
    public string? CurrentRoomId { get; set; }

    /// <summary>
    /// 매칭 상태
    /// </summary>
    [Id(6)]
    public PlayerMatchStatus MatchStatus { get; set; } = PlayerMatchStatus.Idle;

    /// <summary>
    /// 생성일
    /// </summary>
    [Id(7)]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 마지막 활동 시간
    /// </summary>
    [Id(8)]
    public DateTime LastActivityAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// 플레이어 매칭 상태
/// </summary>
[GenerateSerializer]
public enum PlayerMatchStatus
{
    [Id(0)] Idle,           // 대기 중
    [Id(1)] InParty,        // 파티에 속함
    [Id(2)] Matchmaking,    // 매칭 중
    [Id(3)] InRoom,         // 게임 룸에 입장
    [Id(4)] Playing         // 게임 플레이 중
}

