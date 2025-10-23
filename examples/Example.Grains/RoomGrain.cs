using Example.Grains.Interfaces;
using Example.Grains.Models;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Runtime;
using OrleansX.Grains;

namespace Example.Grains;

/// <summary>
/// 게임 룸 Grain 구현 (캐릭터 선택 로직)
/// </summary>
public class RoomGrain : StatefulGrainBase<RoomState>, IRoomGrain
{
    public RoomGrain(
        [PersistentState("room")] IPersistentState<RoomState> state,
        ILogger<RoomGrain> logger)
        : base(state, logger)
    {
    }

    public async Task CreateAsync(List<RoomPlayer> teamA, List<RoomPlayer> teamB, MatchInfo matchInfo)
    {
        await UpdateStateAsync(state =>
        {
            state.RoomId = this.GetPrimaryKeyString();
            state.TeamA = teamA;
            state.TeamB = teamB;
            state.MatchInfo = matchInfo;
            state.Status = RoomStatus.CharacterSelection;
            state.CreatedAt = DateTime.UtcNow;
        });

        Logger.LogInformation("Room {RoomId} created for match {MatchId} (TeamA: {TeamACount}, TeamB: {TeamBCount})",
            State.RoomId, matchInfo.MatchId, teamA.Count, teamB.Count);
    }

    public Task<RoomState?> GetInfoAsync()
    {
        if (!IsStateRecorded)
            return Task.FromResult<RoomState?>(null);

        return Task.FromResult<RoomState?>(State);
    }

    public async Task<bool> SelectCharacterAsync(string playerId, string characterId)
    {
        // 캐릭터 존재 여부 확인
        var character = Characters.GetCharacter(characterId);
        if (character == null)
        {
            Logger.LogWarning("Invalid character: {CharacterId}", characterId);
            return false;
        }

        if (State.Status != RoomStatus.CharacterSelection)
        {
            Logger.LogWarning("Room {RoomId} is not in character selection phase", State.RoomId);
            return false;
        }

        // 플레이어 찾기
        var playerInTeamA = State.TeamA.FirstOrDefault(p => p.PlayerId == playerId);
        var playerInTeamB = State.TeamB.FirstOrDefault(p => p.PlayerId == playerId);
        var player = playerInTeamA ?? playerInTeamB;

        if (player == null)
        {
            Logger.LogWarning("Player {PlayerId} not found in room {RoomId}", playerId, State.RoomId);
            return false;
        }

        // 같은 팀 내에서 캐릭터 중복 체크
        var teamPlayers = playerInTeamA != null ? State.TeamA : State.TeamB;
        
        // 같은 파티가 아닌 경우에만 중복 체크
        var isDuplicate = teamPlayers
            .Where(p => p.PlayerId != playerId) // 자기 자신 제외
            .Where(p => p.PartyId != player.PartyId || p.PartyId == null) // 다른 파티 또는 파티 없음
            .Any(p => p.SelectedCharacterId == characterId);

        if (isDuplicate)
        {
            Logger.LogWarning("Character {CharacterId} already selected by another player in the same team (non-party)",
                characterId);
            return false;
        }

        // 캐릭터 선택
        player.SelectedCharacterId = characterId;
        player.IsReady = false; // 캐릭터 변경 시 준비 상태 초기화

        await SaveStateAsync();

        Logger.LogInformation("Player {PlayerId} selected character {CharacterId} in room {RoomId}",
            playerId, characterId, State.RoomId);
        return true;
    }

    public async Task<bool> SetReadyAsync(string playerId, bool isReady)
    {
        if (State.Status != RoomStatus.CharacterSelection)
        {
            Logger.LogWarning("Room {RoomId} is not in character selection phase", State.RoomId);
            return false;
        }

        var playerInTeamA = State.TeamA.FirstOrDefault(p => p.PlayerId == playerId);
        var playerInTeamB = State.TeamB.FirstOrDefault(p => p.PlayerId == playerId);
        var player = playerInTeamA ?? playerInTeamB;

        if (player == null)
        {
            Logger.LogWarning("Player {PlayerId} not found in room {RoomId}", playerId, State.RoomId);
            return false;
        }

        // 캐릭터를 선택하지 않으면 준비 불가
        if (isReady && string.IsNullOrEmpty(player.SelectedCharacterId))
        {
            Logger.LogWarning("Player {PlayerId} cannot ready without selecting a character", playerId);
            return false;
        }

        player.IsReady = isReady;

        await SaveStateAsync();

        Logger.LogInformation("Player {PlayerId} ready status changed to {IsReady} in room {RoomId}",
            playerId, isReady, State.RoomId);
        return true;
    }

    public Task<bool> CanStartGameAsync()
    {
        if (State.Status != RoomStatus.CharacterSelection)
            return Task.FromResult(false);

        // 모든 플레이어가 캐릭터를 선택하고 준비 완료했는지 확인
        var allReady = State.TeamA.All(p => !string.IsNullOrEmpty(p.SelectedCharacterId) && p.IsReady)
            && State.TeamB.All(p => !string.IsNullOrEmpty(p.SelectedCharacterId) && p.IsReady);

        return Task.FromResult(allReady);
    }

    public async Task<bool> StartGameAsync()
    {
        if (!await CanStartGameAsync())
        {
            Logger.LogWarning("Cannot start game in room {RoomId} - not all players ready", State.RoomId);
            return false;
        }

        await UpdateStateAsync(state =>
        {
            state.Status = RoomStatus.InGame;
            state.GameStartedAt = DateTime.UtcNow;
        });

        // 플레이어들 상태 업데이트
        foreach (var player in State.TeamA.Concat(State.TeamB))
        {
            var playerGrain = GrainFactory.GetGrain<IPlayerGrain>(player.PlayerId);
            await playerGrain.SetMatchStatusAsync(PlayerMatchStatus.Playing);
        }

        Logger.LogInformation("Game started in room {RoomId}", State.RoomId);
        return true;
    }

    public async Task LeaveAsync(string playerId)
    {
        await UpdateStateAsync(state =>
        {
            var playerInTeamA = state.TeamA.FirstOrDefault(p => p.PlayerId == playerId);
            var playerInTeamB = state.TeamB.FirstOrDefault(p => p.PlayerId == playerId);

            if (playerInTeamA != null)
            {
                state.TeamA.Remove(playerInTeamA);
            }
            else if (playerInTeamB != null)
            {
                state.TeamB.Remove(playerInTeamB);
            }
        });

        // 플레이어 상태 업데이트
        var playerGrain = GrainFactory.GetGrain<IPlayerGrain>(playerId);
        await playerGrain.LeaveRoomAsync();

        Logger.LogInformation("Player {PlayerId} left room {RoomId}", playerId, State.RoomId);

        // 룸이 비면 종료
        if (State.TeamA.Count == 0 && State.TeamB.Count == 0)
        {
            await UpdateStateAsync(state =>
            {
                state.Status = RoomStatus.Finished;
            });

            Logger.LogInformation("Room {RoomId} finished (empty)", State.RoomId);
        }
    }
}

