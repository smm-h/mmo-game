using MessagePack;

namespace Game.Shared.Components;

[MessagePackObject]
public struct NetworkId
{
    [Key(0)] public uint Id;

    public NetworkId(uint id)
    {
        Id = id;
    }
}

[MessagePackObject]
public struct NetworkOwner
{
    [Key(0)] public int PeerId;

    public NetworkOwner(int peerId)
    {
        PeerId = peerId;
    }
}

/// <summary>
/// Marks an entity as needing network synchronization
/// </summary>
public struct NetworkDirty { }

/// <summary>
/// Marks an entity as controlled by the local client (for prediction)
/// </summary>
public struct LocallyControlled { }
