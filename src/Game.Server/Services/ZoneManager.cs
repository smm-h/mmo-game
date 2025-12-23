using DefaultEcs;
using Game.Shared.Network;

namespace Game.Server.Services;

public class ZoneInstance
{
    public int ZoneId { get; }
    public int InstanceId { get; }
    public World World { get; }
    public int PlayerCount { get; private set; }

    public ZoneInstance(int zoneId, int instanceId)
    {
        ZoneId = zoneId;
        InstanceId = instanceId;
        World = new World();
    }

    public bool CanAcceptPlayer() => PlayerCount < NetworkConfig.MaxPlayersPerZone;

    public void AddPlayer() => PlayerCount++;
    public void RemovePlayer() => PlayerCount--;
}

public class ZoneManager
{
    private readonly Dictionary<int, List<ZoneInstance>> _zones = new();
    private readonly object _lock = new();
    private int _nextInstanceId = 1;

    public ZoneManager()
    {
        // Create initial zones (horizontal scaling - always running)
        CreateZone(1, "Forest");
        CreateZone(2, "Desert");
        CreateZone(3, "Mountains");
        CreateZone(4, "City");
    }

    private void CreateZone(int zoneId, string name)
    {
        lock (_lock)
        {
            if (!_zones.ContainsKey(zoneId))
            {
                _zones[zoneId] = new List<ZoneInstance>();
            }

            var instance = new ZoneInstance(zoneId, _nextInstanceId++);
            _zones[zoneId].Add(instance);

            Console.WriteLine($"Zone {zoneId} ({name}) instance {instance.InstanceId} created");
        }
    }

    public ZoneInstance? GetZoneForPlayer(int zoneId)
    {
        lock (_lock)
        {
            if (!_zones.TryGetValue(zoneId, out var instances))
                return null;

            // Find instance with capacity
            var instance = instances.FirstOrDefault(i => i.CanAcceptPlayer());

            // If no capacity, create new instance (horizontal scaling)
            if (instance == null)
            {
                instance = new ZoneInstance(zoneId, _nextInstanceId++);
                instances.Add(instance);
                Console.WriteLine($"Zone {zoneId} new instance {instance.InstanceId} created (overflow)");
            }

            return instance;
        }
    }

    public IEnumerable<ZoneInstance> GetAllInstances()
    {
        lock (_lock)
        {
            return _zones.Values.SelectMany(z => z).ToList();
        }
    }

    /// <summary>
    /// Merge two zones if population drops (dynamic scaling)
    /// </summary>
    public bool TryMergeInstances(int zoneId)
    {
        lock (_lock)
        {
            if (!_zones.TryGetValue(zoneId, out var instances) || instances.Count < 2)
                return false;

            var sortedByPopulation = instances.OrderBy(i => i.PlayerCount).ToList();
            var smallest = sortedByPopulation[0];
            var secondSmallest = sortedByPopulation[1];

            // Only merge if combined population fits in one instance
            if (smallest.PlayerCount + secondSmallest.PlayerCount <= NetworkConfig.MaxPlayersPerZone)
            {
                // TODO: Transfer players from smallest to secondSmallest
                // Then remove smallest instance
                return true;
            }

            return false;
        }
    }

    /// <summary>
    /// Split a zone if it's getting full (dynamic scaling)
    /// </summary>
    public ZoneInstance? TrySplitInstance(int zoneId, int instanceId)
    {
        lock (_lock)
        {
            if (!_zones.TryGetValue(zoneId, out var instances))
                return null;

            var instance = instances.FirstOrDefault(i => i.InstanceId == instanceId);
            if (instance == null || instance.PlayerCount < NetworkConfig.MaxPlayersPerZone * 0.8)
                return null;

            // Create new instance for overflow
            var newInstance = new ZoneInstance(zoneId, _nextInstanceId++);
            instances.Add(newInstance);

            Console.WriteLine($"Zone {zoneId} split: new instance {newInstance.InstanceId}");
            return newInstance;
        }
    }
}
