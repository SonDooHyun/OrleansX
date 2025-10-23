using Example.Grains.Interfaces;
using Example.Grains.Models;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Runtime;
using OrleansX.Grains;

namespace Example.Grains;

/// <summary>
/// 플레이어 Grain 구현
/// </summary>
public class PlayerGrain : StatefulGrainBase<PlayerState>, IPlayerGrain
{
    public PlayerGrain(
        [PersistentState("player")] IPersistentState<PlayerState> state,
        ILogger<PlayerGrain> logger)
        : base(state, logger)
    {
    }

    public async Task CreateAsync(string name, int level, int mmr = 1000)
    {
        await UpdateStateAsync(state =>
        {
            state.PlayerId = this.GetPrimaryKeyString();
            state.Name = name;
            state.Level = level;
            state.Mmr = mmr;
            state.MatchStatus = PlayerMatchStatus.Idle;
            state.CreatedAt = DateTime.UtcNow;
            state.LastActivityAt = DateTime.UtcNow;
        });

        Logger.LogInformation("Player created: {PlayerId}, Name: {Name}, MMR: {Mmr}",
            State.PlayerId, State.Name, State.Mmr);
    }

    public Task<PlayerState?> GetInfoAsync()
    {
        if (!IsStateRecorded)
            return Task.FromResult<PlayerState?>(null);

        State.LastActivityAt = DateTime.UtcNow;
        return Task.FromResult<PlayerState?>(State);
    }

    public async Task JoinPartyAsync(string partyId)
    {
        await UpdateStateAsync(state =>
        {
            state.CurrentPartyId = partyId;
            state.MatchStatus = PlayerMatchStatus.InParty;
            state.LastActivityAt = DateTime.UtcNow;
        });

        Logger.LogInformation("Player {PlayerId} joined party {PartyId}",
            State.PlayerId, partyId);
    }

    public async Task LeavePartyAsync()
    {
        var partyId = State.CurrentPartyId;

        await UpdateStateAsync(state =>
        {
            state.CurrentPartyId = null;
            state.MatchStatus = PlayerMatchStatus.Idle;
            state.LastActivityAt = DateTime.UtcNow;
        });

        Logger.LogInformation("Player {PlayerId} left party {PartyId}",
            State.PlayerId, partyId);
    }

    public async Task EnterRoomAsync(string roomId)
    {
        await UpdateStateAsync(state =>
        {
            state.CurrentRoomId = roomId;
            state.MatchStatus = PlayerMatchStatus.InRoom;
            state.LastActivityAt = DateTime.UtcNow;
        });

        Logger.LogInformation("Player {PlayerId} entered room {RoomId}",
            State.PlayerId, roomId);
    }

    public async Task LeaveRoomAsync()
    {
        var roomId = State.CurrentRoomId;

        await UpdateStateAsync(state =>
        {
            state.CurrentRoomId = null;
            state.MatchStatus = State.CurrentPartyId != null 
                ? PlayerMatchStatus.InParty 
                : PlayerMatchStatus.Idle;
            state.LastActivityAt = DateTime.UtcNow;
        });

        Logger.LogInformation("Player {PlayerId} left room {RoomId}",
            State.PlayerId, roomId);
    }

    public async Task SetMatchStatusAsync(PlayerMatchStatus status)
    {
        await UpdateStateAsync(state =>
        {
            state.MatchStatus = status;
            state.LastActivityAt = DateTime.UtcNow;
        });
    }

    public async Task UpdateMmrAsync(int newMmr)
    {
        await UpdateStateAsync(state =>
        {
            state.Mmr = newMmr;
            state.LastActivityAt = DateTime.UtcNow;
        });

        Logger.LogInformation("Player {PlayerId} MMR updated: {OldMmr} -> {NewMmr}",
            State.PlayerId, State.Mmr, newMmr);
    }
}

