using Example.Grains.Models;
using Orleans;

namespace Example.Grains.Interfaces;

/// <summary>
/// 매칭 메이킹 Grain 인터페이스
/// </summary>
public interface IMatchmakingGrain : IGrainWithStringKey
{
    /// <summary>
    /// 파티를 매칭 큐에 추가합니다.
    /// </summary>
    Task<bool> EnqueuePartyAsync(string partyId, int averageRating, int partySize);

    /// <summary>
    /// 파티를 매칭 큐에서 제거합니다.
    /// </summary>
    Task<bool> DequeuePartyAsync(string partyId);

    /// <summary>
    /// 매칭을 시도합니다.
    /// </summary>
    Task<string?> TryMatchAsync();

    /// <summary>
    /// 현재 대기 중인 파티 수를 가져옵니다.
    /// </summary>
    Task<int> GetQueueSizeAsync();

    /// <summary>
    /// 매치 정보를 가져옵니다.
    /// </summary>
    Task<MatchInfo?> GetMatchInfoAsync(string matchId);
}

