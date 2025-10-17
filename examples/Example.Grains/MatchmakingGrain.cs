using Example.Grains.Interfaces;
using Example.Grains.Models;
using Microsoft.Extensions.Logging;
using Orleans.Runtime;
using OrleansX.Grains;

namespace Example.Grains;

/// <summary>
/// 매칭 메이킹 Grain 구현
/// </summary>
public class MatchmakingGrain : StatefulGrainBase<MatchmakingState>, IMatchmakingGrain
{
    private const int RatingTolerance = 200; // 레이팅 허용 범위
    private const int MatchSize = 2; // 한 매치당 필요한 파티 수

    public MatchmakingGrain(
        [PersistentState("matchmaking")] IPersistentState<MatchmakingState> state,
        ILogger<MatchmakingGrain> logger) : base(state, logger)
    {
    }

    public async Task<bool> EnqueuePartyAsync(string partyId, int averageRating, int partySize)
    {
        // 이미 큐에 있는지 확인
        if (State.QueuedParties.Any(p => p.PartyId == partyId))
        {
            Logger.LogWarning("Party already in queue: {PartyId}", partyId);
            return false;
        }

        State.QueuedParties.Add(new QueuedParty
        {
            PartyId = partyId,
            AverageRating = averageRating,
            PartySize = partySize,
            QueuedAt = DateTimeOffset.UtcNow
        });

        await SaveStateAsync();
        Logger.LogInformation("Party enqueued: {PartyId}, Rating: {Rating}, Size: {Size}", 
            partyId, averageRating, partySize);

        // 자동으로 매칭 시도
        await TryMatchAsync();

        return true;
    }

    public async Task<bool> DequeuePartyAsync(string partyId)
    {
        var party = State.QueuedParties.FirstOrDefault(p => p.PartyId == partyId);
        if (party == null)
        {
            return false;
        }

        State.QueuedParties.Remove(party);
        await SaveStateAsync();

        Logger.LogInformation("Party dequeued: {PartyId}", partyId);
        return true;
    }

    public async Task<string?> TryMatchAsync()
    {
        if (State.QueuedParties.Count < MatchSize)
        {
            Logger.LogDebug("Not enough parties in queue: {Count}/{Required}", 
                State.QueuedParties.Count, MatchSize);
            return null;
        }

        // 레이팅이 비슷한 파티들을 찾기
        var orderedParties = State.QueuedParties
            .OrderBy(p => p.QueuedAt) // 대기 시간이 긴 순서
            .ToList();

        for (int i = 0; i < orderedParties.Count - 1; i++)
        {
            var party1 = orderedParties[i];
            var party2 = orderedParties[i + 1];

            var ratingDiff = Math.Abs(party1.AverageRating - party2.AverageRating);

            // 대기 시간에 따라 허용 범위 증가 (대기 시간이 길수록 매칭 범위 확대)
            var waitTime1 = (DateTimeOffset.UtcNow - party1.QueuedAt).TotalSeconds;
            var waitTime2 = (DateTimeOffset.UtcNow - party2.QueuedAt).TotalSeconds;
            var avgWaitTime = (waitTime1 + waitTime2) / 2;
            var adjustedTolerance = RatingTolerance + (int)(avgWaitTime * 10); // 1초당 10 포인트 증가

            if (ratingDiff <= adjustedTolerance)
            {
                // 매치 생성
                var matchId = Guid.NewGuid().ToString();
                var matchInfo = new MatchInfo
                {
                    MatchId = matchId,
                    PartyIds = new List<string> { party1.PartyId, party2.PartyId },
                    CreatedAt = DateTimeOffset.UtcNow,
                    Status = MatchStatus.Preparing
                };

                State.ActiveMatches[matchId] = matchInfo;
                State.QueuedParties.Remove(party1);
                State.QueuedParties.Remove(party2);
                State.LastMatchAttempt = DateTimeOffset.UtcNow;

                await SaveStateAsync();

                // 파티들에게 매치 발견 알림
                var party1Grain = GrainFactory.GetGrain<IPartyGrain>(party1.PartyId);
                var party2Grain = GrainFactory.GetGrain<IPartyGrain>(party2.PartyId);
                await Task.WhenAll(
                    party1Grain.OnMatchFoundAsync(matchId),
                    party2Grain.OnMatchFoundAsync(matchId)
                );

                Logger.LogInformation("Match created: {MatchId} with parties {Party1} and {Party2}", 
                    matchId, party1.PartyId, party2.PartyId);

                return matchId;
            }
        }

        State.LastMatchAttempt = DateTimeOffset.UtcNow;
        await SaveStateAsync();

        Logger.LogDebug("No suitable match found");
        return null;
    }

    public Task<int> GetQueueSizeAsync()
    {
        return Task.FromResult(State.QueuedParties.Count);
    }

    public Task<MatchInfo?> GetMatchInfoAsync(string matchId)
    {
        if (State.ActiveMatches.TryGetValue(matchId, out var matchInfo))
        {
            return Task.FromResult<MatchInfo?>(matchInfo);
        }

        return Task.FromResult<MatchInfo?>(null);
    }
}

