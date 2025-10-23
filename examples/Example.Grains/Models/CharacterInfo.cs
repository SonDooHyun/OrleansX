using Orleans;

namespace Example.Grains.Models;

/// <summary>
/// 캐릭터 정보
/// </summary>
[GenerateSerializer]
public class CharacterInfo
{
    [Id(0)]
    public string CharacterId { get; set; } = string.Empty;

    [Id(1)]
    public string Name { get; set; } = string.Empty;

    [Id(2)]
    public CharacterRole Role { get; set; }

    [Id(3)]
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// 캐릭터 역할
/// </summary>
[GenerateSerializer]
public enum CharacterRole
{
    [Id(0)] Tank,       // 탱커
    [Id(1)] Damage,     // 딜러
    [Id(2)] Support     // 서포터
}

/// <summary>
/// 사용 가능한 캐릭터 목록
/// </summary>
public static class Characters
{
    public static readonly List<CharacterInfo> All = new()
    {
        new CharacterInfo
        {
            CharacterId = "char_tank_01",
            Name = "Iron Knight",
            Role = CharacterRole.Tank,
            Description = "방어력이 높은 전사"
        },
        new CharacterInfo
        {
            CharacterId = "char_tank_02",
            Name = "Stone Guardian",
            Role = CharacterRole.Tank,
            Description = "체력이 높은 수호자"
        },
        new CharacterInfo
        {
            CharacterId = "char_damage_01",
            Name = "Shadow Assassin",
            Role = CharacterRole.Damage,
            Description = "높은 순간 화력을 가진 암살자"
        },
        new CharacterInfo
        {
            CharacterId = "char_damage_02",
            Name = "Fire Mage",
            Role = CharacterRole.Damage,
            Description = "강력한 마법 공격"
        },
        new CharacterInfo
        {
            CharacterId = "char_damage_03",
            Name = "Archer",
            Role = CharacterRole.Damage,
            Description = "원거리 물리 공격"
        },
        new CharacterInfo
        {
            CharacterId = "char_support_01",
            Name = "Holy Priest",
            Role = CharacterRole.Support,
            Description = "팀 힐과 버프"
        },
        new CharacterInfo
        {
            CharacterId = "char_support_02",
            Name = "Bard",
            Role = CharacterRole.Support,
            Description = "공격력 증가 버프"
        }
    };

    public static CharacterInfo? GetCharacter(string characterId)
    {
        return All.FirstOrDefault(c => c.CharacterId == characterId);
    }
}

