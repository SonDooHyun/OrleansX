using Example.Grains.Models;
using Orleans;

namespace Example.Grains.Interfaces;

/// <summary>
/// 매칭메이킹 Grain 인터페이스
/// (싱글톤 Grain - key는 "global")
/// </summary>
public interface IMatchmakingGrain : IGrainWithStringKey
{
    /// <summary>
    /// 개인 매칭 큐에 추가
    /// </summary>
    Task EnqueueSoloAsync(string playerId, int mmr);

    /// <summary>
    /// 파티 매칭 큐에 추가
    /// </summary>
    Task EnqueuePartyAsync(string partyId, int averageMmr, int playerCount, List<string> playerIds);

    /// <summary>
    /// 큐에서 제거
    /// </summary>
    Task DequeueAsync(string id);

    /// <summary>
    /// 매칭 시도
    /// </summary>
    Task<List<CreatedMatch>> TryMatchAsync();

    /// <summary>
    /// 큐 상태 조회
    /// </summary>
    Task<(int soloCount, int partyCount)> GetQueueStatusAsync();

    /// <summary>
    /// 매칭 히스토리 조회
    /// </summary>
    Task<List<CreatedMatch>> GetMatchHistoryAsync(int count = 10);
}

