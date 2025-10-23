using Example.Grains.Models;
using Orleans;

namespace Example.Grains.Interfaces;

/// <summary>
/// 파티 Grain 인터페이스
/// </summary>
public interface IPartyGrain : IGrainWithStringKey
{
    /// <summary>
    /// 파티 생성
    /// </summary>
    Task CreateAsync(string leaderId, string leaderName, int leaderLevel, int leaderMmr, int maxMembers = 5);

    /// <summary>
    /// 파티 정보 조회
    /// </summary>
    Task<PartyState?> GetInfoAsync();

    /// <summary>
    /// 멤버 참가
    /// </summary>
    Task<bool> JoinAsync(string playerId, string playerName, int level, int mmr);

    /// <summary>
    /// 멤버 탈퇴
    /// </summary>
    Task<bool> LeaveAsync(string playerId);

    /// <summary>
    /// 매칭 시작
    /// </summary>
    Task<bool> StartMatchmakingAsync();

    /// <summary>
    /// 매칭 취소
    /// </summary>
    Task CancelMatchmakingAsync();

    /// <summary>
    /// 매치 찾음 (매칭 시스템에서 호출)
    /// </summary>
    Task OnMatchFoundAsync(string roomId, string matchId);

    /// <summary>
    /// 파티 해산
    /// </summary>
    Task DisbandAsync();
}

