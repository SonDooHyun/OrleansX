using Microsoft.Extensions.Logging;
using Orleans.Runtime;
using OrleansX.Grains;

namespace Tutorial.StatefulGrain.Grains;

public class PlayerGrain : StatefulGrainBase<PlayerState>, IPlayerGrain
{
    public PlayerGrain(
        [PersistentState("player")] IPersistentState<PlayerState> state,
        ILogger<PlayerGrain> logger)
        : base(state, logger)
    {
    }

    public Task<PlayerInfo> GetInfoAsync()
    {
        return Task.FromResult(new PlayerInfo
        {
            PlayerId = this.GetPrimaryKeyString(),
            Name = State.Name,
            Level = State.Level,
            Experience = State.Experience,
            Gold = State.Gold,
            CreatedAt = State.CreatedAt,
            LastLoginAt = State.LastLoginAt
        });
    }

    public async Task InitializeAsync(string name)
    {
        if (!string.IsNullOrEmpty(State.Name))
        {
            Logger.LogWarning("Player {PlayerId} already initialized", this.GetPrimaryKeyString());
            return;
        }

        State.Name = name;
        State.CreatedAt = DateTime.UtcNow;
        State.LastLoginAt = DateTime.UtcNow;

        await SaveStateAsync();

        Logger.LogInformation("Player {PlayerId} initialized with name {Name}",
            this.GetPrimaryKeyString(), name);
    }

    public async Task AddExperienceAsync(int amount)
    {
        State.Experience += amount;

        // 레벨업 체크 (100 경험치당 1레벨)
        while (State.Experience >= State.Level * 100)
        {
            State.Experience -= State.Level * 100;
            State.Level++;
            Logger.LogInformation("Player {PlayerId} leveled up to {Level}",
                this.GetPrimaryKeyString(), State.Level);
        }

        await SaveStateAsync();
    }

    public async Task AddGoldAsync(int amount)
    {
        State.Gold += amount;
        await SaveStateAsync();

        Logger.LogInformation("Player {PlayerId} gained {Amount} gold. Total: {Total}",
            this.GetPrimaryKeyString(), amount, State.Gold);
    }

    public async Task UpdateLastLoginAsync()
    {
        State.LastLoginAt = DateTime.UtcNow;
        await SaveStateAsync();
    }
}
