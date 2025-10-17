using Example.Grains.Interfaces;
using Example.Grains.Models;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Runtime;
using OrleansX.Grains;

namespace Example.Grains;

/// <summary>
/// 파티 Grain 구현
/// </summary>
public class PartyGrain : StatefulGrainBase<PartyState>, IPartyGrain
{
    public PartyGrain(
        [PersistentState("party")] IPersistentState<PartyState> state,
        ILogger<PartyGrain> logger) : base(state, logger)
    {
    }

    public async Task<bool> CreateAsync(string leaderId, string leaderName, int maxMembers = 4)
    {
        if (State.PartyId != null && !string.IsNullOrEmpty(State.PartyId))
        {
            Logger.LogWarning("Party already exists: {PartyId}", State.PartyId);
            return false;
        }

        var partyId = this.GetPrimaryKeyString();
        State.PartyId = partyId;
        State.LeaderId = leaderId;
        State.MaxMembers = maxMembers;
        State.Status = PartyStatus.Waiting;
        State.CreatedAt = DateTimeOffset.UtcNow;
        State.UpdatedAt = DateTimeOffset.UtcNow;

        // 리더를 멤버로 추가
        State.Members.Add(new PartyMember
        {
            PlayerId = leaderId,
            PlayerName = leaderName,
            Level = 1,
            JoinedAt = DateTimeOffset.UtcNow
        });

        await SaveStateAsync();
        Logger.LogInformation("Party created: {PartyId} by {LeaderId}", partyId, leaderId);
        return true;
    }

    public async Task<bool> JoinAsync(string playerId, string playerName, int level)
    {
        if (State.PartyId == null || string.IsNullOrEmpty(State.PartyId))
        {
            Logger.LogWarning("Party not found");
            return false;
        }

        if (State.Status != PartyStatus.Waiting)
        {
            Logger.LogWarning("Party is not waiting: {Status}", State.Status);
            return false;
        }

        if (State.Members.Count >= State.MaxMembers)
        {
            Logger.LogWarning("Party is full");
            return false;
        }

        if (State.Members.Any(m => m.PlayerId == playerId))
        {
            Logger.LogWarning("Player already in party: {PlayerId}", playerId);
            return false;
        }

        State.Members.Add(new PartyMember
        {
            PlayerId = playerId,
            PlayerName = playerName,
            Level = level,
            JoinedAt = DateTimeOffset.UtcNow
        });

        State.UpdatedAt = DateTimeOffset.UtcNow;
        await SaveStateAsync();

        Logger.LogInformation("Player joined party: {PlayerId} -> {PartyId}", playerId, State.PartyId);
        return true;
    }

    public async Task<bool> LeaveAsync(string playerId)
    {
        if (State.PartyId == null || string.IsNullOrEmpty(State.PartyId))
        {
            return false;
        }

        var member = State.Members.FirstOrDefault(m => m.PlayerId == playerId);
        if (member == null)
        {
            return false;
        }

        State.Members.Remove(member);
        State.UpdatedAt = DateTimeOffset.UtcNow;

        // 리더가 떠난 경우
        if (playerId == State.LeaderId)
        {
            if (State.Members.Count > 0)
            {
                // 새로운 리더 지정 (첫 번째 멤버)
                State.LeaderId = State.Members[0].PlayerId;
                Logger.LogInformation("New party leader: {LeaderId}", State.LeaderId);
            }
            else
            {
                // 모든 멤버가 떠난 경우 파티 해산
                State.Status = PartyStatus.Disbanded;
            }
        }

        await SaveStateAsync();
        Logger.LogInformation("Player left party: {PlayerId} -> {PartyId}", playerId, State.PartyId);
        return true;
    }

    public async Task<bool> DisbandAsync(string requesterId)
    {
        if (State.PartyId == null || string.IsNullOrEmpty(State.PartyId))
        {
            return false;
        }

        if (requesterId != State.LeaderId)
        {
            Logger.LogWarning("Only leader can disband party: {RequesterId} != {LeaderId}", 
                requesterId, State.LeaderId);
            return false;
        }

        State.Status = PartyStatus.Disbanded;
        State.UpdatedAt = DateTimeOffset.UtcNow;
        await SaveStateAsync();

        Logger.LogInformation("Party disbanded: {PartyId}", State.PartyId);
        return true;
    }

    public Task<PartyState?> GetStateAsync()
    {
        if (State.PartyId == null || string.IsNullOrEmpty(State.PartyId))
        {
            return Task.FromResult<PartyState?>(null);
        }

        return Task.FromResult<PartyState?>(State);
    }

    public async Task<bool> StartMatchmakingAsync()
    {
        if (State.PartyId == null || string.IsNullOrEmpty(State.PartyId))
        {
            return false;
        }

        if (State.Status != PartyStatus.Waiting)
        {
            Logger.LogWarning("Party is not waiting: {Status}", State.Status);
            return false;
        }

        if (State.IsMatchmaking)
        {
            Logger.LogWarning("Party is already in matchmaking");
            return false;
        }

        State.IsMatchmaking = true;
        State.UpdatedAt = DateTimeOffset.UtcNow;
        await SaveStateAsync();

        // 매칭 큐에 등록
        var matchmakingGrain = GrainFactory.GetGrain<IMatchmakingGrain>("default");
        var averageRating = State.Members.Average(m => m.Level * 100); // 간단한 레이팅 계산
        await matchmakingGrain.EnqueuePartyAsync(State.PartyId, (int)averageRating, State.Members.Count);

        Logger.LogInformation("Party started matchmaking: {PartyId}", State.PartyId);
        return true;
    }

    public async Task<bool> CancelMatchmakingAsync()
    {
        if (State.PartyId == null || string.IsNullOrEmpty(State.PartyId))
        {
            return false;
        }

        if (!State.IsMatchmaking)
        {
            return false;
        }

        State.IsMatchmaking = false;
        State.UpdatedAt = DateTimeOffset.UtcNow;
        await SaveStateAsync();

        // 매칭 큐에서 제거
        var matchmakingGrain = GrainFactory.GetGrain<IMatchmakingGrain>("default");
        await matchmakingGrain.DequeuePartyAsync(State.PartyId);

        Logger.LogInformation("Party cancelled matchmaking: {PartyId}", State.PartyId);
        return true;
    }

    public async Task OnMatchFoundAsync(string matchId)
    {
        State.IsMatchmaking = false;
        State.Status = PartyStatus.InGame;
        State.UpdatedAt = DateTimeOffset.UtcNow;
        await SaveStateAsync();

        Logger.LogInformation("Match found for party {PartyId}: {MatchId}", State.PartyId, matchId);
    }
}

