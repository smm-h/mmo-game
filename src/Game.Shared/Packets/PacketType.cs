namespace Game.Shared.Packets;

public enum PacketType : byte
{
    // Connection
    ConnectionRequest = 1,
    ConnectionAccepted = 2,
    ConnectionRejected = 3,
    Disconnect = 4,
    Heartbeat = 5,

    // Authentication
    LoginRequest = 10,
    LoginResponse = 11,

    // Zone management
    ZoneList = 20,
    ZoneJoinRequest = 21,
    ZoneJoinResponse = 22,
    ZoneTransferRequest = 23,
    ZoneTransferResponse = 24,

    // Entity sync
    EntitySpawn = 30,
    EntityDespawn = 31,
    EntityUpdate = 32,
    EntityBatchUpdate = 33,

    // Player input
    PlayerInput = 40,
    PlayerInputAck = 41,

    // Game events
    ChatMessage = 50,
    DamageEvent = 51,
    ItemPickup = 52,

    // Combat
    Shoot = 55,
    ProjectileSpawn = 56,
    ProjectileHit = 57,
    PlayerHit = 58,
    PlayerDeath = 59,
    Roll = 70,
    RollState = 71,

    // Environment
    LampSpawn = 80,
    LampState = 81,

    // Server -> Client state
    WorldSnapshot = 60,
    DeltaSnapshot = 61,
}
