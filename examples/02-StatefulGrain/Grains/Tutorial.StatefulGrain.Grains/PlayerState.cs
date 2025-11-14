using Orleans;

namespace Tutorial.StatefulGrain.Grains;

[GenerateSerializer]
public class PlayerState
{
    [Id(0)]
    public string Name { get; set; } = string.Empty;

    [Id(1)]
    public int Level { get; set; } = 1;

    [Id(2)]
    public int Experience { get; set; } = 0;

    [Id(3)]
    public int Gold { get; set; } = 100;

    [Id(4)]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Id(5)]
    public DateTime LastLoginAt { get; set; } = DateTime.UtcNow;
}
