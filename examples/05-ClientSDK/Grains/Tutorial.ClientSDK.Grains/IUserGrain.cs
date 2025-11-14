using Orleans;

namespace Tutorial.ClientSDK.Grains;

public interface IUserGrain : IGrainWithStringKey
{
    Task<UserInfo> GetInfoAsync();
    Task UpdateNameAsync(string name);
}

[GenerateSerializer]
public class UserInfo
{
    [Id(0)]
    public string UserId { get; set; } = string.Empty;

    [Id(1)]
    public string Name { get; set; } = string.Empty;

    [Id(2)]
    public DateTime UpdatedAt { get; set; }
}
