using Example.Grains.Models;
using Orleans;

namespace Example.Grains.Interfaces;

/// <summary>
/// 파티 Grain 인터페이스
/// </summary>
public interface IPartyGrain : IGrainWithStringKey
{
    /// <summary>
    /// 파티를 생성합니다.
    /// </summary>
    Task<bool> CreateAsync(string leaderId, string leaderName, int maxMembers = 4);

    /// <summary>
    /// 파티에 참가합니다.
    /// </summary>
    Task<bool> JoinAsync(string playerId, string playerName, int level);

    /// <summary>
    /// 파티에서 탈퇴합니다.
    /// </summary>
    Task<bool> LeaveAsync(string playerId);

    /// <summary>
    /// 파티를 해산합니다.
    /// </summary>
    Task<bool> DisbandAsync(string requesterId);

    /// <summary>
    /// 파티 정보를 가져옵니다.
    /// </summary>
    Task<PartyState?> GetStateAsync();

    /// <summary>
    /// 매칭 큐에 등록합니다.
    /// </summary>
    Task<bool> StartMatchmakingAsync();

    /// <summary>
    /// 매칭을 취소합니다.
    /// </summary>
    Task<bool> CancelMatchmakingAsync();

    /// <summary>
    /// 매치가 발견되었음을 알립니다.
    /// </summary>
    Task OnMatchFoundAsync(string matchId);
}

