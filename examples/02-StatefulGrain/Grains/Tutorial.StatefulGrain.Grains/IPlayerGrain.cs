using Orleans;

namespace Tutorial.StatefulGrain.Grains;

public interface IPlayerGrain : IGrainWithStringKey
{
    Task<PlayerInfo> GetInfoAsync();
    Task InitializeAsync(string name);
    Task AddExperienceAsync(int amount);
    Task AddGoldAsync(int amount);
    Task UpdateLastLoginAsync();
}

[GenerateSerializer]
public class PlayerInfo
{
    [Id(0)]
    public string PlayerId { get; set; } = string.Empty;

    [Id(1)]
    public string Name { get; set; } = string.Empty;

    [Id(2)]
    public int Level { get; set; }

    [Id(3)]
    public int Experience { get; set; }

    [Id(4)]
    public int Gold { get; set; }

    [Id(5)]
    public DateTime CreatedAt { get; set; }

    [Id(6)]
    public DateTime LastLoginAt { get; set; }
}
