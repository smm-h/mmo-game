using Game.Shared.Network;
using Game.Shared.Packets;

namespace Game.Client.Core;

public class NetworkClient : IDisposable
{
    private readonly INetworkTransport _transport;
    private bool _connected;

    public event Action? OnConnected;
    public event Action<string>? OnDisconnected;
    public event Action<int, uint, float, float>? OnZoneJoined; // instanceId, playerNetId, spawnX, spawnY
    public event Action<uint, float, float, uint>? OnPlayerUpdate; // netId, x, y, ackSequence (ackSequence only valid for local player)
    public event Action<int>? OnLatencyUpdate;

    public bool IsConnected => _connected;
    public uint LocalPlayerNetId { get; private set; }
    public int Latency { get; private set; }

    public NetworkClient()
    {
        var settings = NetworkSettings.Instance;
        _transport = NetworkTransportFactory.Create(settings.Transport, isServer: false);

        _transport.OnPeerConnected += OnPeerConnectedHandler;
        _transport.OnPeerDisconnected += OnPeerDisconnectedHandler;
        _transport.OnDataReceived += OnDataReceivedHandler;
        _transport.OnLatencyUpdated += OnLatencyUpdatedHandler;

        _transport.Start(0); // Client uses ephemeral port
    }

    public void Connect(string host, int port = 0)
    {
        var settings = NetworkSettings.Instance;
        if (port == 0) port = settings.ServerPort;

        Console.WriteLine($"[Client] Connecting to {host}:{port} using {settings.Transport}");
        _transport.Connect(host, port);
    }

    public void Disconnect()
    {
        _transport.Disconnect();
    }

    public void PollEvents()
    {
        _transport.PollEvents();
    }

    public void JoinZone(int zoneId)
    {
        // [packetType(1)] [zoneId(4)]
        var packet = new byte[5];
        packet[0] = (byte)PacketType.ZoneJoinRequest;
        BitConverter.GetBytes(zoneId).CopyTo(packet, 1);
        _transport.SendToPeer(0, packet, DeliveryType.ReliableOrdered);
    }

    public void SendInput(float moveX, float moveY, bool attack, bool interact, uint sequence)
    {
        // [packetType(1)] [moveX(4)] [moveY(4)] [attack(1)] [interact(1)] [sequence(4)]
        var packet = new byte[15];
        packet[0] = (byte)PacketType.PlayerInput;
        BitConverter.GetBytes(moveX).CopyTo(packet, 1);
        BitConverter.GetBytes(moveY).CopyTo(packet, 5);
        packet[9] = attack ? (byte)1 : (byte)0;
        packet[10] = interact ? (byte)1 : (byte)0;
        BitConverter.GetBytes(sequence).CopyTo(packet, 11);
        _transport.SendToPeer(0, packet, DeliveryType.Sequenced);
    }

    private void OnPeerConnectedHandler(int peerId)
    {
        _connected = true;
        Console.WriteLine("[Client] Connected to server");
        OnConnected?.Invoke();
    }

    private void OnPeerDisconnectedHandler(int peerId, string reason)
    {
        _connected = false;
        Console.WriteLine($"[Client] Disconnected: {reason}");
        OnDisconnected?.Invoke(reason);
    }

    private void OnDataReceivedHandler(int peerId, byte[] data)
    {
        if (data.Length < 1) return;

        var packetType = (PacketType)data[0];

        switch (packetType)
        {
            case PacketType.ZoneJoinResponse:
                HandleZoneJoinResponse(data);
                break;

            case PacketType.WorldSnapshot:
                HandleWorldSnapshot(data);
                break;
        }
    }

    private void HandleZoneJoinResponse(byte[] data)
    {
        // [packetType(1)] [success(1)] [instanceId(4)] [playerNetId(4)] [spawnX(4)] [spawnY(4)]
        if (data.Length < 18) return;

        var success = data[1] == 1;
        if (success)
        {
            var instanceId = BitConverter.ToInt32(data, 2);
            LocalPlayerNetId = BitConverter.ToUInt32(data, 6);
            var spawnX = BitConverter.ToSingle(data, 10);
            var spawnY = BitConverter.ToSingle(data, 14);

            Console.WriteLine($"[Client] Joined zone instance {instanceId}, netId={LocalPlayerNetId}, spawn=({spawnX}, {spawnY})");
            OnZoneJoined?.Invoke(instanceId, LocalPlayerNetId, spawnX, spawnY);
        }
    }

    private void HandleWorldSnapshot(byte[] data)
    {
        // [packetType(1)] [ackSequence(4)] [playerCount(4)] [player1Data...] [player2Data...]
        // playerData: [netId(4)] [x(4)] [y(4)] = 12 bytes each
        if (data.Length < 9) return;

        var ackSequence = BitConverter.ToUInt32(data, 1);
        var playerCount = BitConverter.ToInt32(data, 5);
        var offset = 9;

        for (int i = 0; i < playerCount && offset + 12 <= data.Length; i++)
        {
            var netId = BitConverter.ToUInt32(data, offset);
            var x = BitConverter.ToSingle(data, offset + 4);
            var y = BitConverter.ToSingle(data, offset + 8);
            offset += 12;

            OnPlayerUpdate?.Invoke(netId, x, y, ackSequence);
        }
    }

    private void OnLatencyUpdatedHandler(int peerId, int latencyMs)
    {
        Latency = latencyMs;
        OnLatencyUpdate?.Invoke(latencyMs);
    }

    public void Dispose()
    {
        _transport.Dispose();
    }
}
