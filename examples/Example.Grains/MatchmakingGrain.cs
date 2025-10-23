using Example.Grains.Interfaces;
using Example.Grains.Models;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Runtime;
using OrleansX.Grains;

namespace Example.Grains;

/// <summary>
/// 매칭메이킹 Grain 구현 (MMR 기반 매칭 로직)
/// 싱글톤 Grain - key는 "global"
/// </summary>
public class MatchmakingGrain : StatefulGrainBase<MatchmakingState>, IMatchmakingGrain
{
    private const int TeamSize = 5; // 한 팀당 플레이어 수
    private const int MmrToleranceBase = 100; // 기본 MMR 허용 범위

    public MatchmakingGrain(
        [PersistentState("matchmaking")] IPersistentState<MatchmakingState> state,
        ILogger<MatchmakingGrain> logger)
        : base(state, logger)
    {
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        // 주기적으로 매칭 시도 (5초마다)
        this.RegisterTimer(
            _ => TryMatchAsync(),
            null,
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(5));

        return base.OnActivateAsync(cancellationToken);
    }

    public async Task EnqueueSoloAsync(string playerId, int mmr)
    {
        await UpdateStateAsync(state =>
        {
            state.SoloQueue[playerId] = new MatchRequest
            {
                Id = playerId,
                Type = Models.MatchType.Solo,
                AverageMmr = mmr,
                PlayerCount = 1,
                PlayerIds = new List<string> { playerId },
                EnqueuedAt = DateTime.UtcNow
            };
        });

        Logger.LogInformation("Player {PlayerId} enqueued for solo matchmaking (MMR: {Mmr})",
            playerId, mmr);
    }

    public async Task EnqueuePartyAsync(string partyId, int averageMmr, int playerCount, List<string> playerIds)
    {
        await UpdateStateAsync(state =>
        {
            state.PartyQueue[partyId] = new MatchRequest
            {
                Id = partyId,
                Type = Models.MatchType.Party,
                AverageMmr = averageMmr,
                PlayerCount = playerCount,
                PlayerIds = playerIds,
                EnqueuedAt = DateTime.UtcNow
            };
        });

        Logger.LogInformation("Party {PartyId} enqueued for matchmaking (MMR: {Mmr}, Size: {Size})",
            partyId, averageMmr, playerCount);
    }

    public async Task DequeueAsync(string id)
    {
        await UpdateStateAsync(state =>
        {
            state.SoloQueue.Remove(id);
            state.PartyQueue.Remove(id);
        });

        Logger.LogInformation("Dequeued: {Id}", id);
    }

    public async Task<List<CreatedMatch>> TryMatchAsync()
    {
        var newMatches = new List<CreatedMatch>();

        await UpdateStateAsync(state =>
        {
            // 모든 매칭 요청을 MMR 순으로 정렬
            var allRequests = state.SoloQueue.Values
                .Concat(state.PartyQueue.Values)
                .OrderBy(r => r.AverageMmr)
                .ToList();

            if (allRequests.Count < 2)
            {
                // 매칭하기에 충분한 플레이어가 없음
                return;
            }

            var matched = new HashSet<string>();

            // 매칭 시도
            for (int i = 0; i < allRequests.Count; i++)
            {
                if (matched.Contains(allRequests[i].Id))
                    continue;

                var teamA = new List<MatchRequest> { allRequests[i] };
                var teamASize = allRequests[i].PlayerCount;
                var teamAMmr = allRequests[i].AverageMmr;

                // Team A 구성 (최대 5명)
                for (int j = i + 1; j < allRequests.Count && teamASize < TeamSize; j++)
                {
                    if (matched.Contains(allRequests[j].Id))
                        continue;

                    if (teamASize + allRequests[j].PlayerCount <= TeamSize)
                    {
                        if (IsMmrCompatible(teamAMmr, allRequests[j].AverageMmr, allRequests[j].GetMmrRange()))
                        {
                            teamA.Add(allRequests[j]);
                            teamASize += allRequests[j].PlayerCount;
                            teamAMmr = (int)teamA.SelectMany(r => Enumerable.Repeat(r.AverageMmr, r.PlayerCount)).Average();
                        }
                    }
                }

                // Team A가 완성되지 않으면 스킵
                if (teamASize != TeamSize)
                    continue;

                // Team B 찾기
                var teamB = new List<MatchRequest>();
                var teamBSize = 0;

                for (int j = 0; j < allRequests.Count && teamBSize < TeamSize; j++)
                {
                    if (matched.Contains(allRequests[j].Id) || teamA.Contains(allRequests[j]))
                        continue;

                    if (teamBSize + allRequests[j].PlayerCount <= TeamSize)
                    {
                        if (IsMmrCompatible(teamAMmr, allRequests[j].AverageMmr, allRequests[j].GetMmrRange()))
                        {
                            teamB.Add(allRequests[j]);
                            teamBSize += allRequests[j].PlayerCount;
                        }
                    }
                }

                // Team B가 완성되면 매치 생성
                if (teamBSize == TeamSize)
                {
                var matchId = Guid.NewGuid().ToString();
                    var roomId = Guid.NewGuid().ToString();

                    var teamBMmr = (int)teamB.SelectMany(r => Enumerable.Repeat(r.AverageMmr, r.PlayerCount)).Average();
                    var averageMmr = (teamAMmr + teamBMmr) / 2;

                    var match = new CreatedMatch
                {
                    MatchId = matchId,
                        RoomId = roomId,
                        TeamA = teamA.SelectMany(r => r.PlayerIds).ToList(),
                        TeamB = teamB.SelectMany(r => r.PlayerIds).ToList(),
                        AverageMmr = averageMmr,
                        CreatedAt = DateTime.UtcNow
                    };

                    newMatches.Add(match);
                    state.Matches.Add(match);

                    // 매칭된 요청 표시
                    foreach (var req in teamA.Concat(teamB))
                    {
                        matched.Add(req.Id);
                    }

                    Logger.LogInformation("Match created: {MatchId}, Room: {RoomId}, MMR: {Mmr}",
                        matchId, roomId, averageMmr);
                }
            }

            // 매칭된 요청 큐에서 제거
            foreach (var id in matched)
            {
                state.SoloQueue.Remove(id);
                state.PartyQueue.Remove(id);
            }

            state.LastMatchAttempt = DateTime.UtcNow;

            // 매칭 히스토리는 최근 100개만 유지
            if (state.Matches.Count > 100)
            {
                state.Matches = state.Matches
                    .OrderByDescending(m => m.CreatedAt)
                    .Take(100)
                    .ToList();
            }
        });

        // 매치 생성 후 처리
        foreach (var match in newMatches)
        {
            await CreateRoomAndNotifyAsync(match);
        }

        return newMatches;
    }

    private bool IsMmrCompatible(int mmr1, int mmr2, int tolerance)
    {
        return Math.Abs(mmr1 - mmr2) <= tolerance;
    }

    private async Task CreateRoomAndNotifyAsync(CreatedMatch match)
    {
        try
        {
            // 플레이어 정보 수집
            var teamAPlayers = new List<RoomPlayer>();
            foreach (var playerId in match.TeamA)
            {
                var playerGrain = GrainFactory.GetGrain<IPlayerGrain>(playerId);
                var playerInfo = await playerGrain.GetInfoAsync();
                if (playerInfo != null)
                {
                    teamAPlayers.Add(new RoomPlayer
                    {
                        PlayerId = playerId,
                        PlayerName = playerInfo.Name,
                        Mmr = playerInfo.Mmr,
                        PartyId = playerInfo.CurrentPartyId,
                        JoinedAt = DateTime.UtcNow
                    });
                }
            }

            var teamBPlayers = new List<RoomPlayer>();
            foreach (var playerId in match.TeamB)
            {
                var playerGrain = GrainFactory.GetGrain<IPlayerGrain>(playerId);
                var playerInfo = await playerGrain.GetInfoAsync();
                if (playerInfo != null)
                {
                    teamBPlayers.Add(new RoomPlayer
                    {
                        PlayerId = playerId,
                        PlayerName = playerInfo.Name,
                        Mmr = playerInfo.Mmr,
                        PartyId = playerInfo.CurrentPartyId,
                        JoinedAt = DateTime.UtcNow
                    });
                }
            }

            // 룸 생성
            var roomGrain = GrainFactory.GetGrain<IRoomGrain>(match.RoomId);
            await roomGrain.CreateAsync(teamAPlayers, teamBPlayers, new MatchInfo
            {
                MatchId = match.MatchId,
                AverageMmr = match.AverageMmr,
                MatchedAt = match.CreatedAt
            });

            // 관련 파티들에 알림
            var partyIds = teamAPlayers.Concat(teamBPlayers)
                .Where(p => p.PartyId != null)
                .Select(p => p.PartyId!)
                .Distinct();

            foreach (var partyId in partyIds)
            {
                var partyGrain = GrainFactory.GetGrain<IPartyGrain>(partyId);
                await partyGrain.OnMatchFoundAsync(match.RoomId, match.MatchId);
            }

            // 개인 매칭 플레이어들 상태 업데이트
            foreach (var player in teamAPlayers.Concat(teamBPlayers).Where(p => p.PartyId == null))
            {
                var playerGrain = GrainFactory.GetGrain<IPlayerGrain>(player.PlayerId);
                await playerGrain.EnterRoomAsync(match.RoomId);
            }

            Logger.LogInformation("Room {RoomId} created and notified for match {MatchId}",
                match.RoomId, match.MatchId);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to create room for match {MatchId}", match.MatchId);
        }
    }

    public Task<(int soloCount, int partyCount)> GetQueueStatusAsync()
    {
        return Task.FromResult((State.SoloQueue.Count, State.PartyQueue.Count));
    }

    public Task<List<CreatedMatch>> GetMatchHistoryAsync(int count = 10)
    {
        var history = State.Matches
            .OrderByDescending(m => m.CreatedAt)
            .Take(count)
            .ToList();

        return Task.FromResult(history);
    }
}

