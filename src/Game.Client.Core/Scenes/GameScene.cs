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

    // Input prediction buffer - stores inputs for reconciliation
    private readonly Queue<InputRecord> _inputBuffer = new();
    private const int MaxInputBufferSize = 64;

    // Other players
    private readonly Dictionary<uint, PlayerState> _players = new();

    // Input
    private KeyboardState _prevKeyboard;
    private uint _inputSequence;
    private const float MoveSpeed = 200f;
    private const float TickDelta = 1f / 60f; // Client runs at 60fps

    // UI
    private float _latency;

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
        Console.WriteLine($"[Scene] GameScene entered - Local player netId={_localNetId}");
    }

    public override void Exit()
    {
        NetworkClient.OnPlayerUpdate -= OnPlayerUpdate;
        NetworkClient.OnLatencyUpdate -= OnLatencyUpdate;
        NetworkClient.OnDisconnected -= OnDisconnected;
    }

    private void OnPlayerUpdate(uint netId, float x, float y, uint ackSequence)
    {
        if (netId == _localNetId)
        {
            // Server authoritative position with acknowledged sequence
            _serverX = x;
            _serverY = y;
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
            player.InterpT = 0f;
            player.LastUpdate = DateTime.UtcNow;
        }
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

    public override void Update(GameTime gameTime)
    {
        var dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        var keyboard = Keyboard.GetState();

        // Gather input
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

            // Immediate client-side prediction
            _renderX += moveX * MoveSpeed * dt;
            _renderY += moveY * MoveSpeed * dt;

            // Clamp to world bounds
            _renderX = Math.Clamp(_renderX, 20, 1260);
            _renderY = Math.Clamp(_renderY, 20, 700);
        }

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

        // Draw other players (blue) with interpolation
        foreach (var (netId, player) in _players)
        {
            float drawX = MathHelper.Lerp(player.PrevX, player.X, player.InterpT);
            float drawY = MathHelper.Lerp(player.PrevY, player.Y, player.InterpT);
            DrawPlayer(spriteBatch, pixel, drawX, drawY, Color.CornflowerBlue, false);
        }

        // Draw local player (green)
        DrawPlayer(spriteBatch, pixel, _renderX, _renderY, Color.LimeGreen, true);

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

    private void DrawPlayer(SpriteBatch spriteBatch, Texture2D pixel, float x, float y, Color color, bool isLocal)
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

        // Direction indicator (facing up by default)
        var indicatorRect = new Rectangle((int)(x - 4), (int)(y - size / 2 - 8), 8, 8);
        spriteBatch.Draw(pixel, indicatorRect, Color.Yellow);
    }

    private void DrawUI(SpriteBatch spriteBatch, Texture2D pixel)
    {
        // Top bar
        spriteBatch.Draw(pixel, new Rectangle(0, 0, 1280, 40), new Color(20, 20, 30, 200));

        var font = GameMain.DefaultFont;
        if (font != null)
        {
            spriteBatch.DrawString(font, $"Players: {_players.Count + 1}", new Vector2(20, 8), Color.White);
            spriteBatch.DrawString(font, $"Ping: {_latency}ms", new Vector2(180, 8), Color.White);
            spriteBatch.DrawString(font, $"Pos: ({_renderX:F0}, {_renderY:F0})", new Vector2(320, 8), Color.White);
            spriteBatch.DrawString(font, $"Buffer: {_inputBuffer.Count}", new Vector2(500, 8), Color.Gray);
            spriteBatch.DrawString(font, "WASD to move | ESC to disconnect", new Vector2(800, 8), Color.Gray);
        }
        else
        {
            var playerCountWidth = (_players.Count + 1) * 20;
            spriteBatch.Draw(pixel, new Rectangle(20, 15, playerCountWidth, 10), Color.Green);

            var latencyColor = _latency < 50 ? Color.Green : (_latency < 100 ? Color.Yellow : Color.Red);
            spriteBatch.Draw(pixel, new Rectangle(200, 15, 50, 10), latencyColor);
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
        public float InterpT = 1f;
        public DateTime LastUpdate = DateTime.UtcNow;
    }
}
