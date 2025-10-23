using Example.Grains.Models;
using Orleans;

namespace Example.Grains.Interfaces;

/// <summary>
/// 플레이어 Grain 인터페이스
/// </summary>
public interface IPlayerGrain : IGrainWithStringKey
{
    /// <summary>
    /// 플레이어 생성
    /// </summary>
    Task CreateAsync(string name, int level, int mmr = 1000);

    /// <summary>
    /// 플레이어 정보 조회
    /// </summary>
    Task<PlayerState?> GetInfoAsync();

    /// <summary>
    /// 파티에 참가
    /// </summary>
    Task JoinPartyAsync(string partyId);

    /// <summary>
    /// 파티에서 탈퇴
    /// </summary>
    Task LeavePartyAsync();

    /// <summary>
    /// 룸에 입장
    /// </summary>
    Task EnterRoomAsync(string roomId);

    /// <summary>
    /// 룸에서 퇴장
    /// </summary>
    Task LeaveRoomAsync();

    /// <summary>
    /// 매칭 상태 변경
    /// </summary>
    Task SetMatchStatusAsync(PlayerMatchStatus status);

    /// <summary>
    /// MMR 업데이트
    /// </summary>
    Task UpdateMmrAsync(int newMmr);
}

