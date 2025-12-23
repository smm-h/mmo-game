using Game.Shared.Network;
using Game.Shared.Packets;

namespace Game.Server.Services;

public class NetworkService : IDisposable
{
    private readonly INetworkTransport _transport;
    private readonly ZoneManager _zoneManager;
    private readonly Dictionary<int, PlayerConnection> _connections = new();
    private readonly List<Projectile> _projectiles = new();
    private readonly object _lock = new();
    private uint _nextProjectileId = 1;

    public NetworkService(ZoneManager zoneManager)
    {
        _zoneManager = zoneManager;

        var settings = NetworkSettings.Instance;
        _transport = NetworkTransportFactory.Create(settings.Transport, isServer: true);

        _transport.OnPeerConnected += OnPeerConnected;
        _transport.OnPeerDisconnected += OnPeerDisconnected;
        _transport.OnDataReceived += OnDataReceived;
        _transport.OnLatencyUpdated += OnLatencyUpdated;
    }

    public void Start()
    {
        var settings = NetworkSettings.Instance;
        _transport.Start(settings.ServerPort);
        Console.WriteLine($"[Server] Listening on port {settings.ServerPort} using {settings.Transport}");
    }

    public void Stop()
    {
        _transport.Stop();
    }

    public void PollEvents()
    {
        _transport.PollEvents();
    }

    public void BroadcastToZone(ZoneInstance zone, byte[] data, DeliveryType delivery)
    {
        lock (_lock)
        {
            foreach (var conn in _connections.Values.Where(c => c.Zone == zone))
            {
                _transport.SendToPeer(conn.PeerId, data, delivery);
            }
        }
    }

    private void OnPeerConnected(int peerId)
    {
        lock (_lock)
        {
            var connection = new PlayerConnection(peerId);
            _connections[peerId] = connection;
            Console.WriteLine($"[Server] Player connected: {peerId}");
        }
    }

    private void OnPeerDisconnected(int peerId, string reason)
    {
        lock (_lock)
        {
            if (_connections.TryGetValue(peerId, out var connection))
            {
                connection.Zone?.RemovePlayer();
                _connections.Remove(peerId);
                Console.WriteLine($"[Server] Player disconnected: {peerId} ({reason})");
            }
        }
    }

    private void OnDataReceived(int peerId, byte[] data)
    {
        if (data.Length < 1) return;

        var packetType = (PacketType)data[0];

        lock (_lock)
        {
            if (!_connections.TryGetValue(peerId, out var connection))
                return;

            HandlePacket(connection, packetType, data);
        }
    }

    private void OnLatencyUpdated(int peerId, int latencyMs)
    {
        lock (_lock)
        {
            if (_connections.TryGetValue(peerId, out var conn))
            {
                conn.LatencyMs = latencyMs;
            }
        }
    }

    private void HandlePacket(PlayerConnection connection, PacketType type, byte[] data)
    {
        switch (type)
        {
            case PacketType.ZoneJoinRequest:
                HandleZoneJoin(connection, data);
                break;
            case PacketType.PlayerInput:
                HandlePlayerInput(connection, data);
                break;
            case PacketType.Shoot:
                HandleShoot(connection, data);
                break;
            case PacketType.Heartbeat:
                connection.LastHeartbeat = DateTime.UtcNow;
                break;
        }
    }

    private void HandleZoneJoin(PlayerConnection connection, byte[] data)
    {
        // Simple parsing: [packetType(1)] [zoneId(4)]
        if (data.Length < 5) return;

        var zoneId = BitConverter.ToInt32(data, 1);
        var zone = _zoneManager.GetZoneForPlayer(zoneId);

        // Response: [packetType(1)] [success(1)] [instanceId(4)] [playerNetId(4)] [spawnX(4)] [spawnY(4)]
        var response = new byte[18];
        response[0] = (byte)PacketType.ZoneJoinResponse;

        if (zone != null)
        {
            connection.Zone = zone;
            zone.AddPlayer();
            connection.NetworkId = (uint)(connection.PeerId + 1000); // Simple ID assignment

            response[1] = 1; // success
            BitConverter.GetBytes(zone.InstanceId).CopyTo(response, 2);
            BitConverter.GetBytes(connection.NetworkId).CopyTo(response, 6);
            BitConverter.GetBytes(400f).CopyTo(response, 10); // spawnX
            BitConverter.GetBytes(300f).CopyTo(response, 14); // spawnY

            Console.WriteLine($"[Server] Player {connection.PeerId} joined zone {zoneId} instance {zone.InstanceId}");
        }
        else
        {
            response[1] = 0; // failure
        }

        _transport.SendToPeer(connection.PeerId, response, DeliveryType.ReliableOrdered);
    }

    private void HandlePlayerInput(PlayerConnection connection, byte[] data)
    {
        // [packetType(1)] [moveX(4)] [moveY(4)] [attack(1)] [interact(1)] [sequence(4)]
        if (data.Length < 15 || connection.Zone == null) return;

        var moveX = BitConverter.ToSingle(data, 1);
        var moveY = BitConverter.ToSingle(data, 5);
        var sequence = BitConverter.ToUInt32(data, 11);

        // Use fixed delta per input (client sends at 60fps, so ~16.67ms per input)
        const float inputDelta = 1f / 60f;
        const float moveSpeed = 200f;

        // Update player position (server authoritative)
        connection.PositionX += moveX * moveSpeed * inputDelta;
        connection.PositionY += moveY * moveSpeed * inputDelta;

        // Clamp to world bounds
        connection.PositionX = Math.Clamp(connection.PositionX, 20, 1260);
        connection.PositionY = Math.Clamp(connection.PositionY, 20, 700);

        connection.LastInputSequence = sequence;
    }

    private void HandleShoot(PlayerConnection connection, byte[] data)
    {
        // [packetType(1)] [targetX(4)] [targetY(4)]
        if (data.Length < 9 || connection.Zone == null) return;

        var targetX = BitConverter.ToSingle(data, 1);
        var targetY = BitConverter.ToSingle(data, 5);

        // Calculate direction
        var dx = targetX - connection.PositionX;
        var dy = targetY - connection.PositionY;
        var len = MathF.Sqrt(dx * dx + dy * dy);
        if (len < 1f) return;

        dx /= len;
        dy /= len;

        // Create projectile
        var projectile = new Projectile
        {
            Id = _nextProjectileId++,
            OwnerId = connection.NetworkId,
            Zone = connection.Zone,
            X = connection.PositionX,
            Y = connection.PositionY,
            VelX = dx * 500f, // projectile speed
            VelY = dy * 500f,
            SpawnTime = DateTime.UtcNow
        };
        _projectiles.Add(projectile);

        // Broadcast projectile spawn to zone
        // [packetType(1)] [projId(4)] [ownerId(4)] [x(4)] [y(4)] [velX(4)] [velY(4)]
        var packet = new byte[25];
        packet[0] = (byte)PacketType.ProjectileSpawn;
        BitConverter.GetBytes(projectile.Id).CopyTo(packet, 1);
        BitConverter.GetBytes(projectile.OwnerId).CopyTo(packet, 5);
        BitConverter.GetBytes(projectile.X).CopyTo(packet, 9);
        BitConverter.GetBytes(projectile.Y).CopyTo(packet, 13);
        BitConverter.GetBytes(projectile.VelX).CopyTo(packet, 17);
        BitConverter.GetBytes(projectile.VelY).CopyTo(packet, 21);

        BroadcastToZone(connection.Zone, packet, DeliveryType.ReliableOrdered);
    }

    public void UpdateProjectiles(float dt)
    {
        lock (_lock)
        {
            var toRemove = new List<Projectile>();

            foreach (var proj in _projectiles)
            {
                // Move projectile
                proj.X += proj.VelX * dt;
                proj.Y += proj.VelY * dt;

                // Check bounds
                if (proj.X < 0 || proj.X > 1280 || proj.Y < 0 || proj.Y > 720)
                {
                    toRemove.Add(proj);
                    continue;
                }

                // Check timeout (3 seconds max)
                if ((DateTime.UtcNow - proj.SpawnTime).TotalSeconds > 3)
                {
                    toRemove.Add(proj);
                    continue;
                }

                // Check collision with players
                foreach (var player in _connections.Values)
                {
                    if (player.Zone != proj.Zone) continue;
                    if (player.NetworkId == proj.OwnerId) continue;
                    if (player.Health <= 0) continue;

                    // Simple circle collision (20px radius)
                    var dx = player.PositionX - proj.X;
                    var dy = player.PositionY - proj.Y;
                    var dist = MathF.Sqrt(dx * dx + dy * dy);

                    if (dist < 25f)
                    {
                        // Hit!
                        player.Health -= 20;
                        toRemove.Add(proj);

                        // Broadcast hit
                        // [packetType(1)] [playerId(4)] [health(4)] [shooterId(4)]
                        var hitPacket = new byte[13];
                        hitPacket[0] = (byte)PacketType.PlayerHit;
                        BitConverter.GetBytes(player.NetworkId).CopyTo(hitPacket, 1);
                        BitConverter.GetBytes(player.Health).CopyTo(hitPacket, 5);
                        BitConverter.GetBytes(proj.OwnerId).CopyTo(hitPacket, 9);
                        BroadcastToZone(proj.Zone, hitPacket, DeliveryType.ReliableOrdered);

                        if (player.Health <= 0)
                        {
                            // Death - respawn after delay
                            player.RespawnTime = DateTime.UtcNow.AddSeconds(3);

                            // Broadcast death
                            var deathPacket = new byte[9];
                            deathPacket[0] = (byte)PacketType.PlayerDeath;
                            BitConverter.GetBytes(player.NetworkId).CopyTo(deathPacket, 1);
                            BitConverter.GetBytes(proj.OwnerId).CopyTo(deathPacket, 5);
                            BroadcastToZone(proj.Zone, deathPacket, DeliveryType.ReliableOrdered);
                        }
                        break;
                    }
                }
            }

            foreach (var proj in toRemove)
            {
                _projectiles.Remove(proj);
            }

            // Handle respawns
            foreach (var player in _connections.Values)
            {
                if (player.Health <= 0 && player.RespawnTime.HasValue && DateTime.UtcNow >= player.RespawnTime)
                {
                    player.Health = 100;
                    player.PositionX = 400 + Random.Shared.Next(-100, 100);
                    player.PositionY = 300 + Random.Shared.Next(-100, 100);
                    player.RespawnTime = null;
                }
            }
        }
    }

    public void BroadcastWorldState()
    {
        lock (_lock)
        {
            var zones = _connections.Values
                .Where(c => c.Zone != null)
                .GroupBy(c => c.Zone);

            foreach (var zoneGroup in zones)
            {
                var playersInZone = zoneGroup.ToList();
                if (playersInZone.Count == 0) continue;

                // Send personalized snapshot to each player (includes their ack sequence)
                foreach (var recipient in playersInZone)
                {
                    // Build world state packet
                    // [packetType(1)] [ackSequence(4)] [playerCount(4)] [player1Data...] [player2Data...]
                    // playerData: [netId(4)] [x(4)] [y(4)] [health(4)] = 16 bytes each
                    var packet = new byte[1 + 4 + 4 + (playersInZone.Count * 16)];
                    packet[0] = (byte)PacketType.WorldSnapshot;
                    BitConverter.GetBytes(recipient.LastInputSequence).CopyTo(packet, 1);
                    BitConverter.GetBytes(playersInZone.Count).CopyTo(packet, 5);

                    var offset = 9;
                    foreach (var player in playersInZone)
                    {
                        BitConverter.GetBytes(player.NetworkId).CopyTo(packet, offset);
                        BitConverter.GetBytes(player.PositionX).CopyTo(packet, offset + 4);
                        BitConverter.GetBytes(player.PositionY).CopyTo(packet, offset + 8);
                        BitConverter.GetBytes(player.Health).CopyTo(packet, offset + 12);
                        offset += 16;
                    }

                    _transport.SendToPeer(recipient.PeerId, packet, DeliveryType.Sequenced);
                }
            }
        }
    }

    public void Dispose()
    {
        _transport.Dispose();
    }
}

public class PlayerConnection
{
    public int PeerId { get; }
    public uint NetworkId { get; set; }
    public ZoneInstance? Zone { get; set; }
    public DateTime LastHeartbeat { get; set; } = DateTime.UtcNow;
    public int LatencyMs { get; set; }
    public float PositionX { get; set; } = 400f;
    public float PositionY { get; set; } = 300f;
    public uint LastInputSequence { get; set; }
    public int Health { get; set; } = 100;
    public DateTime? RespawnTime { get; set; }

    public PlayerConnection(int peerId)
    {
        PeerId = peerId;
    }
}

public class Projectile
{
    public uint Id { get; set; }
    public uint OwnerId { get; set; }
    public ZoneInstance? Zone { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public float VelX { get; set; }
    public float VelY { get; set; }
    public DateTime SpawnTime { get; set; }
}
