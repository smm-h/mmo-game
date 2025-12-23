using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Game.Client.Core.Scenes;

public class GameScene : Scene
{
    // Local player state
    private readonly uint _localNetId;
    private float _serverX;      // Last known server position
    private float _serverY;
    private float _renderX;      // Smoothed render position
    private float _renderY;
    private uint _lastServerSequence;  // Last input sequence server acknowledged
    private int _localHealth = 100;

    // Input prediction buffer - stores inputs for reconciliation
    private readonly Queue<InputRecord> _inputBuffer = new();
    private const int MaxInputBufferSize = 64;

    // Other players
    private readonly Dictionary<uint, PlayerState> _players = new();

    // Projectiles
    private readonly Dictionary<uint, ProjectileState> _projectiles = new();

    // Input
    private KeyboardState _prevKeyboard;
    private MouseState _prevMouse;
    private uint _inputSequence;
    private const float MoveSpeed = 200f;
    private const float TickDelta = 1f / 60f; // Client runs at 60fps

    // UI
    private float _latency;
    private string _killFeed = "";
    private float _killFeedTimer;

    // Roll state
    private bool _isRolling;
    private float _rollTimer;
    private float _rollCooldown;
    private const float RollDuration = 1f;
    private const float RollCooldownTime = 2f;
    private const float RollSpeedMultiplier = 2.5f;

    public GameScene(uint localNetId, float spawnX, float spawnY)
    {
        _localNetId = localNetId;
        _serverX = spawnX;
        _serverY = spawnY;
        _renderX = spawnX;
        _renderY = spawnY;
    }

    public override void Enter()
    {
        NetworkClient.OnPlayerUpdate += OnPlayerUpdate;
        NetworkClient.OnLatencyUpdate += OnLatencyUpdate;
        NetworkClient.OnDisconnected += OnDisconnected;
        NetworkClient.OnProjectileSpawn += OnProjectileSpawn;
        NetworkClient.OnPlayerHit += OnPlayerHit;
        NetworkClient.OnPlayerDeath += OnPlayerDeath;
        NetworkClient.OnRollState += OnRollState;
        Console.WriteLine($"[Scene] GameScene entered - Local player netId={_localNetId}");
    }

    public override void Exit()
    {
        NetworkClient.OnPlayerUpdate -= OnPlayerUpdate;
        NetworkClient.OnLatencyUpdate -= OnLatencyUpdate;
        NetworkClient.OnDisconnected -= OnDisconnected;
        NetworkClient.OnProjectileSpawn -= OnProjectileSpawn;
        NetworkClient.OnPlayerHit -= OnPlayerHit;
        NetworkClient.OnPlayerDeath -= OnPlayerDeath;
        NetworkClient.OnRollState -= OnRollState;
    }

    private void OnPlayerUpdate(uint netId, float x, float y, int health, uint ackSequence)
    {
        if (netId == _localNetId)
        {
            // Server authoritative position with acknowledged sequence
            _serverX = x;
            _serverY = y;
            _localHealth = health;
            _lastServerSequence = ackSequence;
            Reconcile();
        }
        else
        {
            // Other players - interpolate
            if (!_players.TryGetValue(netId, out var player))
            {
                player = new PlayerState();
                _players[netId] = player;
            }
            // Store previous for interpolation
            player.PrevX = player.X;
            player.PrevY = player.Y;
            player.X = x;
            player.Y = y;
            player.Health = health;
            player.InterpT = 0f;
            player.LastUpdate = DateTime.UtcNow;
        }
    }

    private void OnProjectileSpawn(uint projId, uint ownerId, float x, float y, float velX, float velY)
    {
        _projectiles[projId] = new ProjectileState
        {
            OwnerId = ownerId,
            X = x,
            Y = y,
            VelX = velX,
            VelY = velY,
            SpawnTime = DateTime.UtcNow
        };
    }

    private void OnPlayerHit(uint playerId, int newHealth, uint shooterId)
    {
        if (playerId == _localNetId)
        {
            _localHealth = newHealth;
        }
        else if (_players.TryGetValue(playerId, out var player))
        {
            player.Health = newHealth;
        }
    }

    private void OnPlayerDeath(uint playerId, uint killerId)
    {
        var victimName = playerId == _localNetId ? "You" : $"Player {playerId}";
        var killerName = killerId == _localNetId ? "You" : $"Player {killerId}";
        _killFeed = $"{killerName} killed {victimName}!";
        _killFeedTimer = 3f;
    }

    private void Reconcile()
    {
        // Remove all acknowledged inputs from buffer
        while (_inputBuffer.Count > 0 && _inputBuffer.Peek().Sequence <= _lastServerSequence)
        {
            _inputBuffer.Dequeue();
        }

        // Re-predict position from server state + unacknowledged inputs
        float predictedX = _serverX;
        float predictedY = _serverY;

        foreach (var input in _inputBuffer)
        {
            predictedX += input.MoveX * MoveSpeed * TickDelta;
            predictedY += input.MoveY * MoveSpeed * TickDelta;
            predictedX = Math.Clamp(predictedX, 20, 1260);
            predictedY = Math.Clamp(predictedY, 20, 700);
        }

        // Check prediction error
        float errorX = predictedX - _renderX;
        float errorY = predictedY - _renderY;
        float error = MathF.Sqrt(errorX * errorX + errorY * errorY);

        if (error > 50f)
        {
            // Large desync: snap immediately
            _renderX = predictedX;
            _renderY = predictedY;
        }
        else if (error > 1f)
        {
            // Small error: smooth correction
            _renderX += (predictedX - _renderX) * 0.3f;
            _renderY += (predictedY - _renderY) * 0.3f;
        }
        // else: error < 1px, no correction needed
    }

    private void OnLatencyUpdate(int latencyMs)
    {
        _latency = latencyMs;
    }

    private void OnDisconnected(string reason)
    {
        SceneManager.SetScene(new ConnectScene());
    }

    private void OnRollState(uint playerId, bool isRolling)
    {
        if (playerId == _localNetId)
        {
            _isRolling = isRolling;
            if (!isRolling)
            {
                _rollCooldown = RollCooldownTime;
            }
        }
        else if (_players.TryGetValue(playerId, out var player))
        {
            player.IsRolling = isRolling;
        }
    }

    public override void Update(GameTime gameTime)
    {
        var dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        var keyboard = Keyboard.GetState();
        var mouse = Mouse.GetState();

        // Update kill feed timer
        if (_killFeedTimer > 0)
            _killFeedTimer -= dt;

        // Update roll timers
        if (_isRolling)
        {
            _rollTimer -= dt;
            if (_rollTimer <= 0)
            {
                _isRolling = false;
                _rollCooldown = RollCooldownTime;
            }
        }
        else if (_rollCooldown > 0)
        {
            _rollCooldown -= dt;
        }

        // Space bar to roll
        if (keyboard.IsKeyDown(Keys.Space) && _prevKeyboard.IsKeyUp(Keys.Space))
        {
            if (_localHealth > 0 && !_isRolling && _rollCooldown <= 0)
            {
                NetworkClient.SendRoll();
                // Optimistic local prediction
                _isRolling = true;
                _rollTimer = RollDuration;
            }
        }

        // Gather movement input
        float moveX = 0, moveY = 0;
        if (keyboard.IsKeyDown(Keys.W) || keyboard.IsKeyDown(Keys.Up)) moveY = -1;
        if (keyboard.IsKeyDown(Keys.S) || keyboard.IsKeyDown(Keys.Down)) moveY = 1;
        if (keyboard.IsKeyDown(Keys.A) || keyboard.IsKeyDown(Keys.Left)) moveX = -1;
        if (keyboard.IsKeyDown(Keys.D) || keyboard.IsKeyDown(Keys.Right)) moveX = 1;

        // Normalize diagonal movement
        if (moveX != 0 && moveY != 0)
        {
            var len = MathF.Sqrt(moveX * moveX + moveY * moveY);
            moveX /= len;
            moveY /= len;
        }

        bool attack = keyboard.IsKeyDown(Keys.Space);
        bool interact = keyboard.IsKeyDown(Keys.E);

        // Only send and predict if there's input
        if (moveX != 0 || moveY != 0 || attack || interact)
        {
            _inputSequence++;

            // Store input for reconciliation
            _inputBuffer.Enqueue(new InputRecord
            {
                Sequence = _inputSequence,
                MoveX = moveX,
                MoveY = moveY,
                Timestamp = DateTime.UtcNow
            });

            // Send input to server
            NetworkClient.SendInput(moveX, moveY, attack, interact, _inputSequence);

            // Immediate client-side prediction (with roll speed boost)
            var speedMultiplier = _isRolling ? RollSpeedMultiplier : 1f;
            _renderX += moveX * MoveSpeed * speedMultiplier * dt;
            _renderY += moveY * MoveSpeed * speedMultiplier * dt;

            // Clamp to world bounds
            _renderX = Math.Clamp(_renderX, 20, 1260);
            _renderY = Math.Clamp(_renderY, 20, 700);
        }

        // Mouse click to shoot (blocked while rolling)
        if (mouse.LeftButton == ButtonState.Pressed && _prevMouse.LeftButton == ButtonState.Released)
        {
            if (_localHealth > 0 && !_isRolling)
            {
                NetworkClient.SendShoot(mouse.X, mouse.Y);
            }
        }

        // Update projectiles locally (client-side prediction)
        var expiredProjectiles = new List<uint>();
        foreach (var (id, proj) in _projectiles)
        {
            proj.X += proj.VelX * dt;
            proj.Y += proj.VelY * dt;

            // Remove if out of bounds or too old
            if (proj.X < 0 || proj.X > 1280 || proj.Y < 0 || proj.Y > 720 ||
                (DateTime.UtcNow - proj.SpawnTime).TotalSeconds > 3)
            {
                expiredProjectiles.Add(id);
            }
        }
        foreach (var id in expiredProjectiles)
            _projectiles.Remove(id);

        // Update other players interpolation
        foreach (var player in _players.Values)
        {
            player.InterpT = Math.Min(1f, player.InterpT + dt * 15f); // Interpolate over ~66ms
        }

        // Escape to disconnect
        if (keyboard.IsKeyDown(Keys.Escape) && _prevKeyboard.IsKeyUp(Keys.Escape))
        {
            NetworkClient.Disconnect();
            SceneManager.SetScene(new ConnectScene());
        }

        // Clean up stale players
        var staleThreshold = DateTime.UtcNow.AddSeconds(-5);
        var staleIds = _players.Where(p => p.Value.LastUpdate < staleThreshold).Select(p => p.Key).ToList();
        foreach (var id in staleIds)
        {
            _players.Remove(id);
        }

        _prevKeyboard = keyboard;
        _prevMouse = mouse;
    }

    public override void Draw(SpriteBatch spriteBatch, GameTime gameTime)
    {
        spriteBatch.Begin();

        var pixel = GameMain.PixelTexture;
        if (pixel == null)
        {
            spriteBatch.End();
            return;
        }

        // Draw ground/grid
        DrawGrid(spriteBatch, pixel);

        // Draw projectiles
        foreach (var proj in _projectiles.Values)
        {
            var projColor = proj.OwnerId == _localNetId ? Color.Yellow : Color.Orange;
            spriteBatch.Draw(pixel, new Rectangle((int)proj.X - 4, (int)proj.Y - 4, 8, 8), projColor);
        }

        // Draw other players (blue, cyan if rolling) with interpolation
        foreach (var (netId, player) in _players)
        {
            float drawX = MathHelper.Lerp(player.PrevX, player.X, player.InterpT);
            float drawY = MathHelper.Lerp(player.PrevY, player.Y, player.InterpT);
            var playerColor = player.IsRolling ? Color.Cyan : Color.CornflowerBlue;
            DrawPlayer(spriteBatch, pixel, drawX, drawY, player.Health, playerColor, false);
        }

        // Draw local player (green) - cyan if rolling - gray if dead
        var localColor = _localHealth <= 0 ? Color.Gray : (_isRolling ? Color.Cyan : Color.LimeGreen);
        DrawPlayer(spriteBatch, pixel, _renderX, _renderY, _localHealth, localColor, true);

        // Draw UI
        DrawUI(spriteBatch, pixel);

        spriteBatch.End();
    }

    private void DrawGrid(SpriteBatch spriteBatch, Texture2D pixel)
    {
        var gridColor = new Color(40, 40, 50);
        for (int x = 0; x < 1280; x += 64)
        {
            spriteBatch.Draw(pixel, new Rectangle(x, 0, 1, 720), gridColor);
        }
        for (int y = 0; y < 720; y += 64)
        {
            spriteBatch.Draw(pixel, new Rectangle(0, y, 1280, 1), gridColor);
        }
    }

    private void DrawPlayer(SpriteBatch spriteBatch, Texture2D pixel, float x, float y, int health, Color color, bool isLocal)
    {
        var size = isLocal ? 40 : 32;
        var rect = new Rectangle((int)(x - size / 2), (int)(y - size / 2), size, size);

        // Body
        spriteBatch.Draw(pixel, rect, color);

        // Border
        var borderColor = isLocal ? Color.White : Color.Black;
        spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, 2), borderColor);
        spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - 2, rect.Width, 2), borderColor);
        spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, 2, rect.Height), borderColor);
        spriteBatch.Draw(pixel, new Rectangle(rect.Right - 2, rect.Y, 2, rect.Height), borderColor);

        // Health bar above player
        var healthBarWidth = size;
        var healthBarHeight = 6;
        var healthBarY = rect.Y - healthBarHeight - 4;
        var healthPercent = Math.Clamp(health / 100f, 0f, 1f);

        // Health bar background (red)
        spriteBatch.Draw(pixel, new Rectangle(rect.X, healthBarY, healthBarWidth, healthBarHeight), Color.DarkRed);
        // Health bar fill (green)
        spriteBatch.Draw(pixel, new Rectangle(rect.X, healthBarY, (int)(healthBarWidth * healthPercent), healthBarHeight), Color.LimeGreen);
        // Health bar border
        spriteBatch.Draw(pixel, new Rectangle(rect.X, healthBarY, healthBarWidth, 1), Color.Black);
        spriteBatch.Draw(pixel, new Rectangle(rect.X, healthBarY + healthBarHeight - 1, healthBarWidth, 1), Color.Black);
    }

    private void DrawUI(SpriteBatch spriteBatch, Texture2D pixel)
    {
        // Top bar
        spriteBatch.Draw(pixel, new Rectangle(0, 0, 1280, 40), new Color(20, 20, 30, 200));

        var font = GameMain.DefaultFont;
        if (font != null)
        {
            spriteBatch.DrawString(font, $"HP: {_localHealth}", new Vector2(20, 8), _localHealth > 30 ? Color.White : Color.Red);
            spriteBatch.DrawString(font, $"Players: {_players.Count + 1}", new Vector2(120, 8), Color.White);
            spriteBatch.DrawString(font, $"Ping: {_latency}ms", new Vector2(260, 8), Color.White);
            spriteBatch.DrawString(font, "WASD move | Click shoot | Space roll | ESC quit", new Vector2(650, 8), Color.Gray);

            // Kill feed
            if (_killFeedTimer > 0 && !string.IsNullOrEmpty(_killFeed))
            {
                var feedColor = Color.Lerp(Color.Red, Color.Transparent, 1f - (_killFeedTimer / 3f));
                spriteBatch.DrawString(font, _killFeed, new Vector2(640 - font.MeasureString(_killFeed).X / 2, 60), feedColor);
            }

            // Death message
            if (_localHealth <= 0)
            {
                var deathMsg = "YOU DIED - Respawning...";
                spriteBatch.DrawString(font, deathMsg, new Vector2(640 - font.MeasureString(deathMsg).X / 2, 350), Color.Red);
            }
        }
        else
        {
            // No font fallback
            var healthWidth = (int)(_localHealth / 100f * 100);
            spriteBatch.Draw(pixel, new Rectangle(20, 15, 100, 10), Color.DarkRed);
            spriteBatch.Draw(pixel, new Rectangle(20, 15, healthWidth, 10), Color.Green);

            var latencyColor = _latency < 50 ? Color.Green : (_latency < 100 ? Color.Yellow : Color.Red);
            spriteBatch.Draw(pixel, new Rectangle(140, 15, 50, 10), latencyColor);
        }
    }

    private struct InputRecord
    {
        public uint Sequence;
        public float MoveX;
        public float MoveY;
        public DateTime Timestamp;
    }

    private class PlayerState
    {
        public float X;
        public float Y;
        public float PrevX;
        public float PrevY;
        public int Health = 100;
        public float InterpT = 1f;
        public DateTime LastUpdate = DateTime.UtcNow;
        public bool IsRolling;
    }

    private class ProjectileState
    {
        public uint OwnerId;
        public float X;
        public float Y;
        public float VelX;
        public float VelY;
        public DateTime SpawnTime;
    }
}
