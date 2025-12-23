using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Game.Client.Core.Scenes;

public class ConnectScene : Scene
{
    private string _status = "Press ENTER to connect to server...";
    private bool _connecting;
    private float _pulseTime;
    private KeyboardState _prevKeyboard;

    public override void Enter()
    {
        NetworkClient.OnConnected += OnConnected;
        NetworkClient.OnDisconnected += OnDisconnected;
        NetworkClient.OnZoneJoined += OnZoneJoined;
        Console.WriteLine("[Scene] ConnectScene entered");
    }

    public override void Exit()
    {
        NetworkClient.OnConnected -= OnConnected;
        NetworkClient.OnDisconnected -= OnDisconnected;
        NetworkClient.OnZoneJoined -= OnZoneJoined;
    }

    private void OnConnected()
    {
        _status = "Connected! Joining zone...";
        NetworkClient.JoinZone(1); // Join forest zone
    }

    private void OnZoneJoined(int instanceId, uint playerNetId, float spawnX, float spawnY)
    {
        SceneManager.SetScene(new GameScene(playerNetId, spawnX, spawnY));
    }

    private void OnDisconnected(string reason)
    {
        _status = $"Disconnected: {reason}\nPress ENTER to reconnect...";
        _connecting = false;
    }

    public override void Update(GameTime gameTime)
    {
        _pulseTime += (float)gameTime.ElapsedGameTime.TotalSeconds;

        var keyboard = Keyboard.GetState();

        if (keyboard.IsKeyDown(Keys.Enter) && _prevKeyboard.IsKeyUp(Keys.Enter) && !_connecting)
        {
            _connecting = true;
            _status = "Connecting...";
            NetworkClient.Connect(Game.Shared.Network.NetworkSettings.Instance.ServerHost);
        }

        if (keyboard.IsKeyDown(Keys.Escape))
        {
            Environment.Exit(0);
        }

        _prevKeyboard = keyboard;
    }

    public override void Draw(SpriteBatch spriteBatch, GameTime gameTime)
    {
        spriteBatch.Begin();

        var pixel = GameMain.PixelTexture;
        if (pixel != null)
        {
            // Draw title box
            var titleRect = new Rectangle(340, 200, 600, 100);
            spriteBatch.Draw(pixel, titleRect, new Color(50, 50, 70));

            // Draw border
            DrawBorder(spriteBatch, pixel, titleRect, Color.CornflowerBlue, 3);

            // Pulsing connect indicator
            var pulse = (float)(Math.Sin(_pulseTime * 3) * 0.5 + 0.5);
            var indicatorColor = _connecting
                ? Color.Lerp(Color.Yellow, Color.Orange, pulse)
                : Color.Lerp(Color.Green, Color.LightGreen, pulse);

            var indicatorRect = new Rectangle(600, 350, 80, 80);
            spriteBatch.Draw(pixel, indicatorRect, indicatorColor);

            // Status text background
            var statusRect = new Rectangle(340, 450, 600, 60);
            spriteBatch.Draw(pixel, statusRect, new Color(40, 40, 50));
            DrawBorder(spriteBatch, pixel, statusRect, Color.Gray, 2);

            // Draw text if font is available
            var font = GameMain.DefaultFont;
            if (font != null)
            {
                var titleText = "MMO Game Client";
                var titleSize = font.MeasureString(titleText);
                spriteBatch.DrawString(font, titleText,
                    new Vector2(640 - titleSize.X / 2, 235), Color.White);

                var statusSize = font.MeasureString(_status);
                spriteBatch.DrawString(font, _status,
                    new Vector2(640 - statusSize.X / 2, 465), Color.LightGray);

                spriteBatch.DrawString(font, "ESC to quit",
                    new Vector2(20, 680), Color.Gray);
            }

            // If no font, draw simple indicators
            if (font == null)
            {
                // Title area
                spriteBatch.Draw(pixel, new Rectangle(440, 230, 400, 40), Color.White);

                // Status indicator
                var statusColor = _connecting ? Color.Yellow : Color.Green;
                spriteBatch.Draw(pixel, new Rectangle(440, 470, 400, 20), statusColor);
            }
        }

        spriteBatch.End();
    }

    private void DrawBorder(SpriteBatch spriteBatch, Texture2D pixel, Rectangle rect, Color color, int thickness)
    {
        // Top
        spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, thickness), color);
        // Bottom
        spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - thickness, rect.Width, thickness), color);
        // Left
        spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, thickness, rect.Height), color);
        // Right
        spriteBatch.Draw(pixel, new Rectangle(rect.Right - thickness, rect.Y, thickness, rect.Height), color);
    }
}
