using Microsoft.Extensions.Logging;
using Orleans.Runtime;
using OrleansX.Grains;

namespace Tutorial.ClientSDK.Grains;

[GenerateSerializer]
public class UserState
{
    [Id(0)]
    public string Name { get; set; } = string.Empty;

    [Id(1)]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class UserGrain : StatefulGrainBase<UserState>, IUserGrain
{
    public UserGrain(
        [PersistentState("user")] IPersistentState<UserState> state,
        ILogger<UserGrain> logger)
        : base(state, logger)
    {
    }

    public Task<UserInfo> GetInfoAsync()
    {
        return Task.FromResult(new UserInfo
        {
            UserId = this.GetPrimaryKeyString(),
            Name = State.Name,
            UpdatedAt = State.UpdatedAt
        });
    }

    public async Task UpdateNameAsync(string name)
    {
        State.Name = name;
        State.UpdatedAt = DateTime.UtcNow;
        await SaveStateAsync();

        Logger.LogInformation("User {UserId} name updated to {Name}",
            this.GetPrimaryKeyString(), name);
    }
}
