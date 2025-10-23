using Example.Grains.Interfaces;
using Example.Grains.Models;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Runtime;
using Orleans.Transactions.Abstractions;
using OrleansX.Grains;

namespace Example.Grains;

/// <summary>
/// 파티 Grain 구현 (트랜잭션 사용)
/// </summary>
public class PartyGrain : TransactionalGrainBase<PartyState>, IPartyGrain
{
    public PartyGrain(
        [TransactionalState("party")] ITransactionalState<PartyState> state,
        ILogger<PartyGrain> logger)
        : base(state, logger)
    {
    }

    [Transaction(TransactionOption.Create)]
    public async Task CreateAsync(string leaderId, string leaderName, int leaderLevel, int leaderMmr, int maxMembers = 5)
    {
        await UpdateStateAsync(state =>
        {
            state.PartyId = this.GetPrimaryKeyString();
            state.LeaderId = leaderId;
            state.MaxMembers = maxMembers;
            state.Status = PartyStatus.Waiting;
            state.CreatedAt = DateTime.UtcNow;

            // 리더를 첫 멤버로 추가
            state.Members.Add(new PartyMember
            {
                PlayerId = leaderId,
                PlayerName = leaderName,
                Level = leaderLevel,
                Mmr = leaderMmr,
                JoinedAt = DateTime.UtcNow
            });

            state.AverageMmr = leaderMmr;
        });

        var partyId = this.GetPrimaryKeyString();

        // 플레이어 상태 업데이트
        var playerGrain = GrainFactory.GetGrain<IPlayerGrain>(leaderId);
        await playerGrain.JoinPartyAsync(partyId);

        Logger.LogInformation("Party created: {PartyId}, Leader: {LeaderId}",
            partyId, leaderId);
    }

    public async Task<PartyState?> GetInfoAsync()
    {
        try
        {
            return await GetStateAsync();
        }
        catch
        {
            return null;
        }
    }

    [Transaction(TransactionOption.Join)]
    public async Task<bool> JoinAsync(string playerId, string playerName, int level, int mmr)
    {
        return await UpdateStateAsync(state =>
        {
            if (state.Members.Count >= state.MaxMembers)
            {
                Logger.LogWarning("Party {PartyId} is full", state.PartyId);
                return false;
            }

            if (state.Members.Any(m => m.PlayerId == playerId))
            {
                Logger.LogWarning("Player {PlayerId} already in party {PartyId}", playerId, state.PartyId);
                return false;
            }

            if (state.Status != PartyStatus.Waiting)
            {
                Logger.LogWarning("Party {PartyId} is not in Waiting status", state.PartyId);
                return false;
            }

            state.Members.Add(new PartyMember
            {
                PlayerId = playerId,
                PlayerName = playerName,
                Level = level,
                Mmr = mmr,
                JoinedAt = DateTime.UtcNow
            });

            // 평균 MMR 재계산
            state.AverageMmr = (int)state.Members.Average(m => m.Mmr);

            Logger.LogInformation("Player {PlayerId} joined party {PartyId}", playerId, state.PartyId);
            return true;
        });
    }

    [Transaction(TransactionOption.Join)]
    public async Task<bool> LeaveAsync(string playerId)
    {
        return await UpdateStateAsync(state =>
        {
            var member = state.Members.FirstOrDefault(m => m.PlayerId == playerId);
            if (member == null)
            {
                Logger.LogWarning("Player {PlayerId} not in party {PartyId}", playerId, state.PartyId);
                return false;
            }

            state.Members.Remove(member);

            // 리더가 나간 경우
            if (state.LeaderId == playerId)
            {
                if (state.Members.Count > 0)
                {
                    // 첫 번째 멤버를 새 리더로
                    state.LeaderId = state.Members[0].PlayerId;
                    Logger.LogInformation("New leader for party {PartyId}: {NewLeaderId}",
                        state.PartyId, state.LeaderId);
                }
                else
                {
                    // 마지막 멤버가 나가면 파티 해산
                    state.Status = PartyStatus.Disbanded;
                    Logger.LogInformation("Party {PartyId} disbanded (no members)", state.PartyId);
                }
            }

            // 평균 MMR 재계산
            if (state.Members.Count > 0)
            {
                state.AverageMmr = (int)state.Members.Average(m => m.Mmr);
            }

            Logger.LogInformation("Player {PlayerId} left party {PartyId}", playerId, state.PartyId);
            return true;
        });
    }

    public async Task<bool> StartMatchmakingAsync()
    {
        var state = await GetStateAsync();

        if (state.Status != PartyStatus.Waiting)
        {
            Logger.LogWarning("Party {PartyId} cannot start matchmaking (status: {Status})",
                state.PartyId, state.Status);
            return false;
        }

        if (state.Members.Count == 0)
        {
            Logger.LogWarning("Party {PartyId} has no members", state.PartyId);
            return false;
        }

        // 매칭 시스템에 큐 추가
        var matchmakingGrain = GrainFactory.GetGrain<IMatchmakingGrain>("global");
        await matchmakingGrain.EnqueuePartyAsync(
            state.PartyId,
            state.AverageMmr,
            state.Members.Count,
            state.Members.Select(m => m.PlayerId).ToList());

        await UpdateStateAsync(s =>
        {
            s.Status = PartyStatus.Matchmaking;
            s.IsMatchmaking = true;
        });

        // 멤버들 상태 업데이트
        foreach (var member in state.Members)
        {
            var playerGrain = GrainFactory.GetGrain<IPlayerGrain>(member.PlayerId);
            await playerGrain.SetMatchStatusAsync(PlayerMatchStatus.Matchmaking);
        }

        Logger.LogInformation("Party {PartyId} started matchmaking (MMR: {Mmr})",
            state.PartyId, state.AverageMmr);
        return true;
    }

    public async Task CancelMatchmakingAsync()
    {
        var state = await GetStateAsync();

        if (!state.IsMatchmaking)
        {
            return;
        }

        var matchmakingGrain = GrainFactory.GetGrain<IMatchmakingGrain>("global");
        await matchmakingGrain.DequeueAsync(state.PartyId);

        await UpdateStateAsync(s =>
        {
            s.Status = PartyStatus.Waiting;
            s.IsMatchmaking = false;
        });

        // 멤버들 상태 업데이트
        foreach (var member in state.Members)
        {
            var playerGrain = GrainFactory.GetGrain<IPlayerGrain>(member.PlayerId);
            await playerGrain.SetMatchStatusAsync(PlayerMatchStatus.InParty);
        }

        Logger.LogInformation("Party {PartyId} cancelled matchmaking", state.PartyId);
    }

    public async Task OnMatchFoundAsync(string roomId, string matchId)
    {
        var state = await GetStateAsync();

        await UpdateStateAsync(s =>
        {
            s.Status = PartyStatus.InRoom;
            s.IsMatchmaking = false;
        });

        // 멤버들을 룸에 입장시킴
        foreach (var member in state.Members)
        {
            var playerGrain = GrainFactory.GetGrain<IPlayerGrain>(member.PlayerId);
            await playerGrain.EnterRoomAsync(roomId);
        }

        Logger.LogInformation("Party {PartyId} matched! Room: {RoomId}, Match: {MatchId}",
            state.PartyId, roomId, matchId);
    }

    [Transaction(TransactionOption.Join)]
    public async Task DisbandAsync()
    {
        var state = await GetStateAsync();

        // 매칭 중이면 취소
        if (state.IsMatchmaking)
        {
            await CancelMatchmakingAsync();
        }

        // 모든 멤버 파티 탈퇴 처리
        foreach (var member in state.Members.ToList())
        {
            var playerGrain = GrainFactory.GetGrain<IPlayerGrain>(member.PlayerId);
            await playerGrain.LeavePartyAsync();
        }

        await UpdateStateAsync(s =>
        {
            s.Members.Clear();
            s.Status = PartyStatus.Disbanded;
        });

        Logger.LogInformation("Party {PartyId} disbanded by leader", state.PartyId);
    }
}

