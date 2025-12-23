namespace Game.Shared.Network;

/// <summary>
/// ENet transport implementation.
///
/// To enable ENet:
/// 1. Add NuGet package: ENet-CSharp
/// 2. Uncomment the implementation below
/// 3. Bundle native libraries for your platforms
///
/// Native libraries needed:
/// - Windows: enet.dll
/// - Linux: libenet.so
/// - macOS: libenet.dylib
/// </summary>
public class ENetTransport : INetworkTransport
{
    private readonly bool _isServer;
    private bool _isRunning;

    public bool IsRunning => _isRunning;
    public int ConnectedPeersCount => 0; // TODO: Implement

    public event Action<int>? OnPeerConnected;
    public event Action<int, string>? OnPeerDisconnected;
    public event Action<int, byte[]>? OnDataReceived;
    public event Action<int, int>? OnLatencyUpdated;

    public ENetTransport(bool isServer)
    {
        _isServer = isServer;

        // Uncomment when ENet-CSharp is added:
        // ENet.Library.Initialize();
    }

    public void Start(int port)
    {
        ThrowNotImplemented();
        _isRunning = true;

        /* Uncomment when ENet-CSharp is added:

        _host = new Host();

        if (_isServer)
        {
            var address = new Address { Port = (ushort)port };
            _host.Create(address, NetworkConfig.MaxPlayersPerZone, 2);
        }
        else
        {
            _host.Create();
        }
        */
    }

    public void Stop()
    {
        _isRunning = false;

        /* Uncomment when ENet-CSharp is added:
        _host?.Dispose();
        _host = null;
        */
    }

    public void Connect(string host, int port)
    {
        ThrowNotImplemented();

        /* Uncomment when ENet-CSharp is added:
        var address = new Address();
        address.SetHost(host);
        address.Port = (ushort)port;
        _serverPeer = _host.Connect(address, 2);
        */
    }

    public void Disconnect()
    {
        /* Uncomment when ENet-CSharp is added:
        _serverPeer?.Disconnect(0);
        */
    }

    public void PollEvents()
    {
        /* Uncomment when ENet-CSharp is added:

        Event netEvent;
        while (_host.Service(0, out netEvent) > 0)
        {
            switch (netEvent.Type)
            {
                case EventType.Connect:
                    OnPeerConnected?.Invoke((int)netEvent.Peer.ID);
                    break;

                case EventType.Disconnect:
                    OnPeerDisconnected?.Invoke((int)netEvent.Peer.ID, "Disconnected");
                    break;

                case EventType.Receive:
                    var data = new byte[netEvent.Packet.Length];
                    netEvent.Packet.CopyTo(data);
                    netEvent.Packet.Dispose();
                    OnDataReceived?.Invoke((int)netEvent.Peer.ID, data);
                    break;
            }
        }
        */
    }

    public void SendToAll(byte[] data, DeliveryType delivery)
    {
        ThrowNotImplemented();

        /* Uncomment when ENet-CSharp is added:
        var packet = default(Packet);
        packet.Create(data, ToENetFlags(delivery));
        _host.Broadcast(0, ref packet);
        */
    }

    public void SendToPeer(int peerId, byte[] data, DeliveryType delivery)
    {
        ThrowNotImplemented();

        /* Uncomment when ENet-CSharp is added:
        // Need to track peers by ID
        */
    }

    /* Uncomment when ENet-CSharp is added:
    private static PacketFlags ToENetFlags(DeliveryType type) => type switch
    {
        DeliveryType.Unreliable => PacketFlags.None,
        DeliveryType.Reliable => PacketFlags.Reliable,
        DeliveryType.ReliableOrdered => PacketFlags.Reliable,
        DeliveryType.Sequenced => PacketFlags.UnsequencedFragment,
        _ => PacketFlags.Reliable
    };
    */

    private static void ThrowNotImplemented()
    {
        throw new NotImplementedException(
            "ENet transport not enabled. Add ENet-CSharp NuGet package and uncomment implementation. " +
            "See ENetTransport.cs for instructions.");
    }

    public void Dispose()
    {
        Stop();
        // ENet.Library.Deinitialize();
    }
}
