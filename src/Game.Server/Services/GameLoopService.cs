using System.Diagnostics;
using Game.Shared.Network;

namespace Game.Server.Services;

public class GameLoopService : BackgroundService
{
    private readonly ZoneManager _zoneManager;
    private readonly NetworkService _networkService;
    private readonly ILogger<GameLoopService> _logger;
    private int _tickCounter;

    public GameLoopService(
        ZoneManager zoneManager,
        NetworkService networkService,
        ILogger<GameLoopService> logger)
    {
        _zoneManager = zoneManager;
        _networkService = networkService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Game loop starting at {TickRate} Hz", NetworkConfig.TickRate);

        var stopwatch = Stopwatch.StartNew();
        var accumulator = 0.0;
        var lastTime = stopwatch.Elapsed.TotalMilliseconds;

        while (!stoppingToken.IsCancellationRequested)
        {
            var currentTime = stopwatch.Elapsed.TotalMilliseconds;
            var frameTime = currentTime - lastTime;
            lastTime = currentTime;

            accumulator += frameTime;

            // Process network events
            _networkService.PollEvents();

            // Fixed timestep game loop
            while (accumulator >= NetworkConfig.TickDeltaMs)
            {
                Tick();
                accumulator -= NetworkConfig.TickDeltaMs;
            }

            // Sleep to prevent spinning
            var sleepTime = Math.Max(1, (int)(NetworkConfig.TickDeltaMs - (stopwatch.Elapsed.TotalMilliseconds - currentTime)));
            await Task.Delay(sleepTime, stoppingToken);
        }

        _networkService.Stop();
        _logger.LogInformation("Game loop stopped");
    }

    private void Tick()
    {
        foreach (var zone in _zoneManager.GetAllInstances())
        {
            TickZone(zone);
        }

        // Broadcast world state every 3 ticks (~66ms at 20Hz)
        _tickCounter++;
        if (_tickCounter >= 3)
        {
            _networkService.BroadcastWorldState();
            _tickCounter = 0;
        }
    }

    private void TickZone(ZoneInstance zone)
    {
        // TODO: Run DefaultEcs systems for game logic
        // Movement, collision, etc. handled via ECS
    }
}
