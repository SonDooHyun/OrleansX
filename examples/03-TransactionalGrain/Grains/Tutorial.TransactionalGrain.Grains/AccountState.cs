using Orleans;

namespace Tutorial.TransactionalGrain.Grains;

[GenerateSerializer]
public class AccountState
{
    [Id(0)]
    public string AccountId { get; set; } = string.Empty;

    [Id(1)]
    public string Owner { get; set; } = string.Empty;

    [Id(2)]
    public decimal Balance { get; set; } = 0;

    [Id(3)]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Id(4)]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
