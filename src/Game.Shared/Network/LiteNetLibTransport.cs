using LiteNetLib;
using LiteNetLib.Utils;

namespace Game.Shared.Network;

public class LiteNetLibTransport : INetworkTransport, INetEventListener
{
    private readonly NetManager _netManager;
    private readonly bool _isServer;
    private NetPeer? _serverPeer; // For client mode

    public bool IsRunning => _netManager.IsRunning;
    public int ConnectedPeersCount => _netManager.ConnectedPeersCount;

    public event Action<int>? OnPeerConnected;
    public event Action<int, string>? OnPeerDisconnected;
    public event Action<int, byte[]>? OnDataReceived;
    public event Action<int, int>? OnLatencyUpdated;

    public LiteNetLibTransport(bool isServer)
    {
        _isServer = isServer;
        _netManager = new NetManager(this)
        {
            AutoRecycle = true,
            UpdateTime = 15,
            DisconnectTimeout = NetworkConfig.ConnectionTimeoutMs,
            UnsyncedEvents = false
        };
    }

    public void Start(int port)
    {
        if (_isServer)
        {
            _netManager.Start(port);
        }
        else
        {
            _netManager.Start();
        }
    }

    public void Stop()
    {
        _netManager.Stop();
    }

    public void Connect(string host, int port)
    {
        _netManager.Connect(host, port, NetworkConfig.ConnectionKey);
    }

    public void Disconnect()
    {
        _serverPeer?.Disconnect();
        _netManager.Stop();
    }

    public void PollEvents()
    {
        _netManager.PollEvents();
    }

    public void SendToAll(byte[] data, DeliveryType delivery)
    {
        _netManager.SendToAll(data, ToLiteNetDelivery(delivery));
    }

    public void SendToPeer(int peerId, byte[] data, DeliveryType delivery)
    {
        if (_isServer)
        {
            var peer = _netManager.GetPeerById(peerId);
            peer?.Send(data, ToLiteNetDelivery(delivery));
        }
        else
        {
            // Client sends to server
            _serverPeer?.Send(data, ToLiteNetDelivery(delivery));
        }
    }

    private static DeliveryMethod ToLiteNetDelivery(DeliveryType type) => type switch
    {
        DeliveryType.Unreliable => DeliveryMethod.Unreliable,
        DeliveryType.Reliable => DeliveryMethod.ReliableUnordered,
        DeliveryType.ReliableOrdered => DeliveryMethod.ReliableOrdered,
        DeliveryType.Sequenced => DeliveryMethod.Sequenced,
        _ => DeliveryMethod.ReliableOrdered
    };

    // INetEventListener implementation (explicit to avoid naming conflicts)
    void INetEventListener.OnConnectionRequest(ConnectionRequest request)
    {
        if (_isServer)
        {
            if (_netManager.ConnectedPeersCount < 10000)
                request.AcceptIfKey(NetworkConfig.ConnectionKey);
            else
                request.Reject();
        }
    }

    void INetEventListener.OnPeerConnected(NetPeer peer)
    {
        if (!_isServer)
            _serverPeer = peer;

        OnPeerConnected?.Invoke(peer.Id);
    }

    void INetEventListener.OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
    {
        if (!_isServer)
            _serverPeer = null;

        OnPeerDisconnected?.Invoke(peer.Id, disconnectInfo.Reason.ToString());
    }

    void INetEventListener.OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channelNumber, DeliveryMethod deliveryMethod)
    {
        var data = new byte[reader.AvailableBytes];
        reader.GetBytes(data, data.Length);
        OnDataReceived?.Invoke(peer.Id, data);
    }

    void INetEventListener.OnNetworkError(System.Net.IPEndPoint endPoint, System.Net.Sockets.SocketError socketError)
    {
        Console.WriteLine($"[LiteNetLib] Network error from {endPoint}: {socketError}");
    }

    void INetEventListener.OnNetworkReceiveUnconnected(System.Net.IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType)
    {
    }

    void INetEventListener.OnNetworkLatencyUpdate(NetPeer peer, int latency)
    {
        OnLatencyUpdated?.Invoke(peer.Id, latency);
    }

    public void Dispose()
    {
        Stop();
    }
}
