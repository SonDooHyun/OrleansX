using Example.Grains.Models;
using Orleans;

namespace Example.Grains.Interfaces;

/// <summary>
/// 게임 룸 Grain 인터페이스
/// </summary>
public interface IRoomGrain : IGrainWithStringKey
{
    /// <summary>
    /// 룸 생성 (매칭 완료 후 호출)
    /// </summary>
    Task CreateAsync(List<RoomPlayer> teamA, List<RoomPlayer> teamB, MatchInfo matchInfo);

    /// <summary>
    /// 룸 정보 조회
    /// </summary>
    Task<RoomState?> GetInfoAsync();

    /// <summary>
    /// 캐릭터 선택
    /// </summary>
    Task<bool> SelectCharacterAsync(string playerId, string characterId);

    /// <summary>
    /// 준비 상태 변경
    /// </summary>
    Task<bool> SetReadyAsync(string playerId, bool isReady);

    /// <summary>
    /// 게임 시작 가능 여부 확인
    /// </summary>
    Task<bool> CanStartGameAsync();

    /// <summary>
    /// 게임 시작
    /// </summary>
    Task<bool> StartGameAsync();

    /// <summary>
    /// 플레이어 퇴장
    /// </summary>
    Task LeaveAsync(string playerId);
}

