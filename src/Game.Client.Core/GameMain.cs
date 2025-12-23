using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Game.Client.Core.Scenes;

namespace Game.Client.Core;

public class GameMain : Microsoft.Xna.Framework.Game
{
    private GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch = null!;
    private SceneManager _sceneManager = null!;
    private NetworkClient _networkClient = null!;

    // Shared rendering resources
    public static Texture2D? PixelTexture { get; private set; }
    public static SpriteFont? DefaultFont { get; private set; }

    public GameMain()
    {
        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        IsMouseVisible = true;

        _graphics.PreferredBackBufferWidth = 1280;
        _graphics.PreferredBackBufferHeight = 720;
        IsFixedTimeStep = true;
        TargetElapsedTime = TimeSpan.FromSeconds(1.0 / 60.0);
    }

    protected override void Initialize()
    {
        _networkClient = new NetworkClient();
        _sceneManager = new SceneManager(this, _networkClient);

        base.Initialize();
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);

        // Create a 1x1 white pixel texture for drawing shapes
        PixelTexture = new Texture2D(GraphicsDevice, 1, 1);
        PixelTexture.SetData(new[] { Color.White });

        // Try to load font, fall back to null if not available
        try
        {
            DefaultFont = Content.Load<SpriteFont>("Fonts/Default");
        }
        catch
        {
            Console.WriteLine("[Client] No font loaded - text will not be displayed");
            DefaultFont = null;
        }

        _sceneManager.LoadContent(Content);
        _sceneManager.SetScene(new ConnectScene());
    }

    protected override void Update(GameTime gameTime)
    {
        _networkClient.PollEvents();
        _sceneManager.Update(gameTime);
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(new Color(30, 30, 40)); // Dark background
        _sceneManager.Draw(_spriteBatch, gameTime);
        base.Draw(gameTime);
    }

    protected override void OnExiting(object sender, ExitingEventArgs args)
    {
        _networkClient.Dispose();
        base.OnExiting(sender, args);
    }
}
