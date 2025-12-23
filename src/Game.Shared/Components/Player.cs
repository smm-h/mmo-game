using MessagePack;

namespace Game.Shared.Components;

[MessagePackObject]
public struct PlayerData
{
    [Key(0)] public string Name;
    [Key(1)] public int Health;
    [Key(2)] public int MaxHealth;

    public PlayerData(string name, int maxHealth)
    {
        Name = name;
        Health = maxHealth;
        MaxHealth = maxHealth;
    }
}

[MessagePackObject]
public struct InputState
{
    [Key(0)] public float MoveX;
    [Key(1)] public float MoveY;
    [Key(2)] public bool Attack;
    [Key(3)] public bool Interact;
    [Key(4)] public uint SequenceNumber;

    public bool HasMovement => MoveX != 0 || MoveY != 0;
}

/// <summary>
/// Current zone the entity is in
/// </summary>
[MessagePackObject]
public struct ZoneId
{
    [Key(0)] public int Id;

    public ZoneId(int id)
    {
        Id = id;
    }
}
