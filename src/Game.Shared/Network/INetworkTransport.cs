namespace Game.Shared.Network;

/// <summary>
/// Abstraction layer for network transport (LiteNetLib, ENet, etc.)
/// Switch implementations via configuration without changing game code.
/// </summary>
public interface INetworkTransport : IDisposable
{
    bool IsRunning { get; }
    int ConnectedPeersCount { get; }

    void Start(int port);
    void Stop();
    void PollEvents();

    // Client-side
    void Connect(string host, int port);
    void Disconnect();

    // Sending
    void SendToAll(byte[] data, DeliveryType delivery);
    void SendToPeer(int peerId, byte[] data, DeliveryType delivery);

    // Events
    event Action<int>? OnPeerConnected;           // peerId
    event Action<int, string>? OnPeerDisconnected; // peerId, reason
    event Action<int, byte[]>? OnDataReceived;     // peerId, data
    event Action<int, int>? OnLatencyUpdated;      // peerId, latencyMs
}

public enum DeliveryType
{
    /// <summary>Fast, no guarantee of delivery or order</summary>
    Unreliable,

    /// <summary>Guaranteed delivery, no order guarantee</summary>
    Reliable,

    /// <summary>Guaranteed delivery and order</summary>
    ReliableOrdered,

    /// <summary>No delivery guarantee, but maintains order (drops old)</summary>
    Sequenced
}

public enum TransportType
{
    LiteNetLib,
    ENet
}

public static class NetworkTransportFactory
{
    public static INetworkTransport Create(TransportType type, bool isServer)
    {
        return type switch
        {
            TransportType.LiteNetLib => new LiteNetLibTransport(isServer),
            TransportType.ENet => new ENetTransport(isServer),
            _ => throw new ArgumentException($"Unknown transport type: {type}")
        };
    }
}
