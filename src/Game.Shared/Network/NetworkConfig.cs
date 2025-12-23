namespace Game.Shared.Network;

public static class NetworkConfig
{
    /// <summary>
    /// Server tick rate in Hz
    /// </summary>
    public const int TickRate = 20;

    /// <summary>
    /// Milliseconds per tick
    /// </summary>
    public const float TickDeltaMs = 1000f / TickRate;

    /// <summary>
    /// Seconds per tick
    /// </summary>
    public const float TickDelta = 1f / TickRate;

    /// <summary>
    /// Default server port
    /// </summary>
    public const int DefaultPort = 7777;

    /// <summary>
    /// Maximum players per zone instance
    /// </summary>
    public const int MaxPlayersPerZone = 150;

    /// <summary>
    /// Connection timeout in milliseconds
    /// </summary>
    public const int ConnectionTimeoutMs = 10000;

    /// <summary>
    /// Heartbeat interval in milliseconds
    /// </summary>
    public const int HeartbeatIntervalMs = 1000;

    /// <summary>
    /// Maximum packet size in bytes
    /// </summary>
    public const int MaxPacketSize = 1400; // MTU safe

    /// <summary>
    /// Number of input states to keep for reconciliation
    /// </summary>
    public const int InputBufferSize = 64;

    /// <summary>
    /// Connection key for LiteNetLib
    /// </summary>
    public const string ConnectionKey = "MMOGame_v1";
}
